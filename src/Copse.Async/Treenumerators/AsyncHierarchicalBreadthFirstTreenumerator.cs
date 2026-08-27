using Copse.Core;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Treenumerators
{
  // Direct style over the shared color-agnostic BreadthFirstPathState, with the child/root
  // pulls PROBED so a pull that completes inline costs no state machine at all (the fast-path
  // probe idiom -- see AsyncToSync).
  //
  // The BFS engine's async wrinkle: a ref parameter and a ref local are both illegal in the
  // async continuations a pending pull resumes through, so the sync driver's single
  // ref-parameter seam splits into two parameterless probed seams (one over the schedule-stack
  // top, one over the queue front) and the ref-local front is inlined as repeated _Path.Front
  // access (semantically identical -- Front returns a ref to the same slot). This is the one
  // restructuring the async port imposes on the engines.
  /// <summary>
  /// The breadth-first engine cursor over a hierarchical source: roots from an async stream,
  /// children pulled per node through <typeparamref name="TAsyncChildEnumerator"/>. Normally
  /// constructed by <c>AsyncHierarchicalTreenumerable</c> rather than directly.
  /// </summary>
  internal sealed class AsyncHierarchicalBreadthFirstTreenumerator<TNode, THandle, TAsyncChildEnumerator>
    : IAsyncTreenumerator<TNode>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<THandle>
  {
    /// <summary>Builds the traversal from the pull topology: the roots, a factory producing
    /// each handle's child enumerator, and the map resolving a handle to its node.</summary>
    public AsyncHierarchicalBreadthFirstTreenumerator(
      IAsyncEnumerable<THandle> roots,
      Func<NodeContext<THandle>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<THandle, TNode> handleToNodeMap)
    {
      _RootsEnumerator = roots.GetAsyncEnumerator();
      _Path = new BreadthFirstPathState<TNode, THandle, TAsyncChildEnumerator>(childEnumeratorFactory, handleToNodeMap);

    }

    private readonly IAsyncEnumerator<THandle> _RootsEnumerator;
    private BreadthFirstPathState<TNode, THandle, TAsyncChildEnumerator> _Path;


    private bool _Finished;
    private bool _RootsEnumeratorFinished = false;
    private bool _RootsScheduled = false;

    /// <inheritdoc/>
    public TNode Node { get; private set; } = default;
    /// <inheritdoc/>
    public int VisitCount { get; private set; } = 0;
    /// <inheritdoc/>
    public TreenumeratorMode Mode { get; private set; } = default;
    /// <inheritdoc/>
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    // NOT async, and neither are the helpers below: every pull is PROBED, and a pull that
    // completes inline stays ordinary method calls with no state machine -- the fast-path probe
    // idiom (see AsyncToSync). A pull ADVANCES its cursor, so a pending pull resumes through a
    // continuation that CONSUMES the pulled result; Advance's loop state lives entirely in
    // fields, so the schedule continuations then perform the loop's between-iteration mutation
    // and re-enter Advance -- exactly its `continue`.
    /// <inheritdoc/>
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
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.PruneSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.PruneSubtree))
      {
        _Path.PopScheduleStack();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.PruneDescendants))
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
    private bool TrySchedulePulledChildOfScheduleTop(Option<HandleAndSiblingIndex<THandle>> result)
    {
      if (!result.HasValue)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.ScheduleTop.Position.Depth, result.Value.Handle, result.Value.SiblingIndex));
      return true;
    }

    // Land a pulled child under the queue front; false when the enumerator was exhausted.
    private bool TrySchedulePulledChildOfFront(Option<HandleAndSiblingIndex<THandle>> result)
    {
      if (!result.HasValue)
        return false;

      Publish(ref _Path.PushScheduledChild(_Path.Front.Position.Depth, result.Value.Handle, result.Value.SiblingIndex));
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

    private async ValueTask<bool> AwaitThenScheduleChildOfScheduleTopAsync(ValueTask<Option<HandleAndSiblingIndex<THandle>>> pendingPull)
    {
      return TrySchedulePulledChildOfScheduleTop(await pendingPull.ConfigureAwait(false));
    }

    private async ValueTask<bool> AwaitThenScheduleChildOfFrontAsync(ValueTask<Option<HandleAndSiblingIndex<THandle>>> pendingPull)
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
      Mode = TreenumeratorModes.FromVisitCount(frame.VisitCount);
      Node = frame.Node;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
      _Path.Dispose();
      await _RootsEnumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
