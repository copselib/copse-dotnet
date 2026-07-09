using Copse.Core;
using Copse.Core.Async;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Breadth-first <b>async</b> treenumerator: the direct-style async port of
  /// <c>Copse.Treenumerators.BreadthFirstTreenumerator</c>, over the shared
  /// <see cref="BreadthFirstPathState{TNode, TEnumerator}"/>, with the child/root pulls awaited.
  ///
  /// <para><b>The BFS engine's async wrinkle.</b> The sync driver's seam is
  /// <c>TryScheduleNextChildOf(ref BreadthFirstFrame parent)</c> -- a <b>ref parameter</b> -- and it
  /// binds <c>ref var front = ref _Path.Front</c> -- a <b>ref local</b>. Both are illegal in async
  /// methods. So the single ref-parameter seam splits into two parameterless awaited seams (one over the
  /// schedule-stack top, one over the queue front) and the ref-local front is inlined as repeated
  /// <c>_Path.Front</c> access (semantically identical -- Front returns a ref to the same slot). This is
  /// the one restructuring the async port imposes on the engines; everything else mirrors the sync driver.</para>
  /// </summary>
  public sealed class AsyncBreadthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerator<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncBreadthFirstTreenumerator(
      IAsyncEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetAsyncEnumerator();
      _Path = new BreadthFirstPathState<TNode, TAsyncChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IAsyncEnumerator<TNode> _RootsEnumerator;
    private BreadthFirstPathState<TNode, TAsyncChildEnumerator> _Path;
    private readonly Func<TNode, TValue> _Map;

    private bool _Finished;
    private bool _RootsEnumeratorFinished = false;
    private bool _RootsScheduled = false;

    public TValue Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      if (Mode == TreenumeratorMode.SchedulingNode && _Path.HasScheduledNode)
        ApplyStrategy(nodeTraversalStrategies);

      var moved = await Advance().ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private async ValueTask<bool> Advance()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the schedule-stack top.
        if (_Path.HasScheduledNode)
        {
          if (await TryScheduleNextChildOfScheduleTopAsync().ConfigureAwait(false))
            return true;

          _Path.PopScheduleStack();
          continue;
        }

        // 2) Schedule the next root.
        if (!_RootsScheduled)
        {
          if (await TryScheduleNextRootAsync().ConfigureAwait(false))
            return true;

          _RootsScheduled = true;
          _Path.ClearSlotCarry();
          continue;
        }

        if (_Path.QueueIsEmpty)
          return false;

        // 3) Visit the active parent (queue front) and drive its children. Inlines _Path.Front.
        if (_Path.Front.VisitCount == 0)
        {
          _Path.Front.VisitCount = 1;
          Publish(ref _Path.Front);
          return true;
        }

        if (_Path.FrontSlotEnqueuedNode)
        {
          _Path.ClearSlotCarry();
          _Path.Front.VisitCount++;
          Publish(ref _Path.Front);
          return true;
        }

        if (await TryScheduleNextChildOfFrontAsync().ConfigureAwait(false))
          return true;

        _Path.RetireFront();
      }
    }

    private void ApplyStrategy(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
      {
        _Path.PopScheduleStack();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.DisposeScheduleTopEnumerator();

      _Path.AcceptScheduledNode();
    }

    // THE SEAM (schedule-stack top): awaited child pull. Reads the parent (ScheduleTop) inline instead
    // of via a ref parameter, which async methods forbid.
    private async ValueTask<bool> TryScheduleNextChildOfScheduleTopAsync()
    {
      var result = await _Path.ScheduleTop.ChildEnumerator.MoveNextAsync().ConfigureAwait(false);
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.ScheduleTop.Position.Depth, result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    // THE SEAM (queue front): awaited child pull.
    private async ValueTask<bool> TryScheduleNextChildOfFrontAsync()
    {
      var result = await _Path.Front.ChildEnumerator.MoveNextAsync().ConfigureAwait(false);
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.Front.Position.Depth, result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    private async ValueTask<bool> TryScheduleNextRootAsync()
    {
      if (_RootsEnumeratorFinished || !await _RootsEnumerator.MoveNextAsync().ConfigureAwait(false))
        return false;

      Publish(ref _Path.PushScheduledRoot(_RootsEnumerator.Current));
      return true;
    }

    private void Publish(ref BreadthFirstFrame<TNode, TAsyncChildEnumerator> frame)
    {
      Mode = frame.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(frame.Node);
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    public async ValueTask DisposeAsync()
    {
      _Path.Dispose();
      await _RootsEnumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
