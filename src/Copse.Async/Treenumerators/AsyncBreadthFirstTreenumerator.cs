using Copse.Core;
using Copse.Core.Async;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Breadth-first <b>async</b> treenumerator: the direct-style async port of the original
  /// hand-written sync engine, over the shared
  /// <see cref="BreadthFirstPathState{TNode, TEnumerator}"/>, with the child/root pulls PROBED
  /// so a pull that completes inline costs no state machine at all (the fast-path probe idiom --
  /// see AsyncToSync).
  ///
  /// <para><b>The BFS engine's async wrinkle.</b> The original sync driver's seam was
  /// <c>TryScheduleNextChildOf(ref BreadthFirstFrame parent)</c> -- a <b>ref parameter</b> -- and it
  /// bound <c>ref var front = ref _Path.Front</c> -- a <b>ref local</b>. Both are illegal in the async
  /// continuations a pending pull resumes through. So the single ref-parameter seam splits into two
  /// parameterless probed seams (one over the schedule-stack top, one over the queue front) and the
  /// ref-local front is inlined as repeated <c>_Path.Front</c> access (semantically identical -- Front
  /// returns a ref to the same slot). This is the one restructuring the async port imposes on the
  /// engines; everything else mirrors the original sync driver.</para>
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

    // NOT async, and neither are the helpers below: every pull is PROBED, and a pull that
    // completes inline stays ordinary method calls with no state machine -- the fast-path probe
    // idiom (see AsyncToSync). A pull ADVANCES its cursor, so a pending pull resumes through a
    // continuation that CONSUMES the pulled result; Advance's loop state lives entirely in
    // fields, so the schedule continuations then perform the loop's between-iteration mutation
    // and re-enter Advance -- exactly its `continue`.
    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return new ValueTask<bool>(false);

      if (Mode == TreenumeratorMode.SchedulingNode && _Path.HasScheduledNode)
        ApplyStrategy(nodeTraversalStrategies);

      var moved = AdvanceAsync();

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenFinishMoveNextAsync(moved);

      if (!moved.Result)
        _Finished = true;

      return new ValueTask<bool>(moved.Result);
    }

    private ValueTask<bool> AdvanceAsync()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the schedule-stack top.
        if (_Path.HasScheduledNode)
        {
          var scheduled = TryScheduleNextChildOfScheduleTopAsync();

          if (!scheduled.IsCompletedSuccessfully)
            return AwaitScheduleTopPullThenAdvanceAsync(scheduled);

          if (scheduled.Result)
            return new ValueTask<bool>(true);

          _Path.PopScheduleStack();
          continue;
        }

        // 2) Schedule the next root.
        if (!_RootsScheduled)
        {
          var scheduled = TryScheduleNextRootAsync();

          if (!scheduled.IsCompletedSuccessfully)
            return AwaitRootPullThenAdvanceAsync(scheduled);

          if (scheduled.Result)
            return new ValueTask<bool>(true);

          _RootsScheduled = true;
          _Path.ClearSlotCarry();
          continue;
        }

        if (_Path.QueueIsEmpty)
          return new ValueTask<bool>(false);

        // 3) Visit the active parent (queue front) and drive its children. Inlines _Path.Front.
        if (_Path.Front.VisitCount == 0)
        {
          _Path.Front.VisitCount = 1;
          Publish(ref _Path.Front);
          return new ValueTask<bool>(true);
        }

        if (_Path.FrontSlotEnqueuedNode)
        {
          _Path.ClearSlotCarry();
          _Path.Front.VisitCount++;
          Publish(ref _Path.Front);
          return new ValueTask<bool>(true);
        }

        var frontScheduled = TryScheduleNextChildOfFrontAsync();

        if (!frontScheduled.IsCompletedSuccessfully)
          return AwaitFrontPullThenAdvanceAsync(frontScheduled);

        if (frontScheduled.Result)
          return new ValueTask<bool>(true);

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

    // THE SEAM (schedule-stack top): the child pull, probed. Reads the parent (ScheduleTop) inline
    // instead of via a ref parameter (see the class doc). The pulled result lands through the same
    // consume helper whether the pull answered inline or through the pending continuation.
    private ValueTask<bool> TryScheduleNextChildOfScheduleTopAsync()
    {
      var result = _Path.ScheduleTop.ChildEnumerator.MoveNextAsync();

      if (!result.IsCompletedSuccessfully)
        return AwaitThenScheduleChildOfScheduleTopAsync(result);

      return new ValueTask<bool>(TrySchedulePulledChildOfScheduleTop(result.Result));
    }

    // THE SEAM (queue front): the child pull, probed.
    private ValueTask<bool> TryScheduleNextChildOfFrontAsync()
    {
      var result = _Path.Front.ChildEnumerator.MoveNextAsync();

      if (!result.IsCompletedSuccessfully)
        return AwaitThenScheduleChildOfFrontAsync(result);

      return new ValueTask<bool>(TrySchedulePulledChildOfFront(result.Result));
    }

    private ValueTask<bool> TryScheduleNextRootAsync()
    {
      if (_RootsEnumeratorFinished)
        return new ValueTask<bool>(false);

      var moved = _RootsEnumerator.MoveNextAsync();

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenScheduleRootAsync(moved);

      if (!moved.Result)
        return new ValueTask<bool>(false);

      Publish(ref _Path.PushScheduledRoot(_RootsEnumerator.Current));
      return new ValueTask<bool>(true);
    }

    // Land a pulled child under the schedule-stack top; false when the enumerator was exhausted.
    private bool TrySchedulePulledChildOfScheduleTop(ChildResult<TNode> result)
    {
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.ScheduleTop.Position.Depth, result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    // Land a pulled child under the queue front; false when the enumerator was exhausted.
    private bool TrySchedulePulledChildOfFront(ChildResult<TNode> result)
    {
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.Front.Position.Depth, result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    // codegen: begin async-only
    //
    // The suspension continuations. Every pull ADVANCES its cursor, so each continuation CONSUMES
    // the pulled result (through the same consume helper as the fast path) rather than re-entering
    // the probing method. The Advance-level continuations await the pending schedule; a schedule
    // that came up empty owes the loop's between-iteration mutation, after which re-entering
    // Advance is exactly its `continue` (the loop keeps no locals -- its state is all fields).
    private async ValueTask<bool> AwaitThenFinishMoveNextAsync(ValueTask<bool> pendingMove)
    {
      var moved = await pendingMove.ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private async ValueTask<bool> AwaitScheduleTopPullThenAdvanceAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _Path.PopScheduleStack();

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitRootPullThenAdvanceAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _RootsScheduled = true;
      _Path.ClearSlotCarry();

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitFrontPullThenAdvanceAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _Path.RetireFront();

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenScheduleChildOfScheduleTopAsync(ValueTask<ChildResult<TNode>> pendingPull)
    {
      return TrySchedulePulledChildOfScheduleTop(await pendingPull.ConfigureAwait(false));
    }

    private async ValueTask<bool> AwaitThenScheduleChildOfFrontAsync(ValueTask<ChildResult<TNode>> pendingPull)
    {
      return TrySchedulePulledChildOfFront(await pendingPull.ConfigureAwait(false));
    }

    private async ValueTask<bool> AwaitThenScheduleRootAsync(ValueTask<bool> pendingMove)
    {
      if (!await pendingMove.ConfigureAwait(false))
        return false;

      Publish(ref _Path.PushScheduledRoot(_RootsEnumerator.Current));
      return true;
    }
    // codegen: end async-only

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
