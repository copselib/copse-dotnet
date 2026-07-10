using Copse.Core;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Breadth-first <b>async</b> treenumerator over a level-order store, and the codegen source of
  /// truth for its sync twin: strip the <c>await</c> on the store's grow calls and it becomes the
  /// synchronous driver. Native playback for the flat family -- the visit queue's contents are
  /// consecutive store indices and children become available in the order the growing store appends
  /// them, a strictly sequential read. Grow calls are awaited so a store still capturing from an
  /// async feed fills just in time; GetFirstChildIndex/GetValue stay sync.
  /// </summary>
  public sealed class AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, TStore>
    : AsyncTreenumeratorBase<TValue>
    where TStore : IAsyncLevelOrderStore<TValue>
  {
    public AsyncLevelOrderStoreBreadthFirstTreenumerator(TStore store)
    {
      _Store = store;
      _VisitQueue = new RefSemiDeque<Frame>();
      _ScheduleStack = new RefSemiDeque<Frame>();
    }

    // Non-readonly so interface calls on a struct store mutate it in place rather than a
    // defensive copy.
    private TStore _Store;

    // Accepted nodes, scheduled but not yet fully visited. The front is the active parent.
    private readonly RefSemiDeque<Frame> _VisitQueue;
    // The node being classified, plus any SkipNode'd ancestors whose children are being promoted.
    private readonly RefSemiDeque<Frame> _ScheduleStack;

    private int _RootsSeen;
    private bool _RootsFinished;
    private bool _RootsScheduled;
    // True when the front parent's in-progress child slot has enqueued at least one accepted node.
    private bool _SlotCarry;

    private struct Frame
    {
      public int NodeIndex;
      public NodePosition Position;
      public int VisitCount;
      public int NextChildOrdinal;
      public bool ChildrenDisabled; // SkipDescendants/SkipSiblings: yield no more children.
    }

    // NOT async, and neither are the helpers below: every store grow is PROBED, and the pull
    // stays ordinary method calls whenever the store answers inline. Only a genuinely pending
    // grow enters an async continuation -- see the fast-path probe idiom note in AsyncToSync.
    protected override ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // A strategy only applies to the node just scheduled (an empty stack means we have not
      // scheduled anything yet -- the very first move). Visiting nodes ignore the strategy.
      if (Mode == TreenumeratorMode.SchedulingNode && _ScheduleStack.Count > 0)
        ApplyStrategy(nodeTraversalStrategies);

      return Advance();
    }

    // Produce the next single visit (or false when the traversal is exhausted). A pending
    // schedule resumes through a continuation that performs this loop's between-iteration
    // mutation and RE-ENTERS Advance -- re-entry IS the `continue` (all loop state lives in
    // fields).
    private ValueTask<bool> Advance()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the node on top of the schedule stack.
        if (_ScheduleStack.Count > 0)
        {
          var scheduled = TryScheduleNextChildOf(fromVisitQueueFront: false);

          if (!scheduled.IsCompletedSuccessfully)
            return AwaitScheduleThenRetireStackTopAsync(scheduled);

          if (scheduled.Result)
            return new ValueTask<bool>(true);

          _ScheduleStack.RemoveLast();
          continue;
        }

        // 2) Schedule the next root (the forest's children -- no surrounding visits).
        if (!_RootsScheduled)
        {
          var scheduled = TryScheduleNextRoot();

          if (!scheduled.IsCompletedSuccessfully)
            return AwaitScheduleThenFinishRootsAsync(scheduled);

          if (scheduled.Result)
            return new ValueTask<bool>(true);

          _RootsScheduled = true;
          // Enqueues made while scheduling roots have no owing parent; clear the carry.
          _SlotCarry = false;
          continue;
        }

        if (_VisitQueue.Count == 0)
          return new ValueTask<bool>(false);

        // 3) Visit the active parent and drive its children. The two visit-emitting cases mutate
        // the front and return before any probe, so their ref is block-scoped off the calls below.
        {
          ref var front = ref _VisitQueue.GetFirst();

          if (front.VisitCount == 0)
          {
            // Initial visit, before any child is scheduled.
            front.VisitCount = 1;
            PublishVisit(ref front);
            return new ValueTask<bool>(true);
          }

          if (_SlotCarry)
          {
            // The child slot that just finished enqueued at least one node, so the parent is visited.
            _SlotCarry = false;
            front.VisitCount++;
            PublishVisit(ref front);
            return new ValueTask<bool>(true);
          }
        }

        var frontScheduled = TryScheduleNextChildOf(fromVisitQueueFront: true);

        if (!frontScheduled.IsCompletedSuccessfully)
          return AwaitScheduleThenRetireFrontAsync(frontScheduled);

        if (frontScheduled.Result)
          return new ValueTask<bool>(true);

        // The parent has no more children: retire it. The next turn visits the new front.
        _VisitQueue.RemoveFirst();
      }
    }

    // Classify the node just scheduled (the schedule-stack top) by the consumer's strategy.
    private void ApplyStrategy(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (SkipRemainingSiblings())
          _RootsFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an
      // all-bits test), so it must be checked first -- otherwise it would route into the SkipNode
      // promotion path and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
      {
        // Erase the node and its subtree; the slot enqueues nothing.
        _ScheduleStack.RemoveLast();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        // Keep the node resident so Advance can promote its children into its slot.
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        // Accept the node but give it no children, then fall through to the accept below.
        _ScheduleStack.GetLast().ChildrenDisabled = true;

      // Accept (TraverseAll, or the SkipDescendants fall-through): move the node onto the visit
      // queue, and record that this child slot enqueued an accepted node.
      _VisitQueue.AddLast(_ScheduleStack.RemoveLast());
      _SlotCarry = true;
    }

    // THE SEAM, span-arithmetic edition: the given parent's next child is child ordinal
    // NextChildOrdinal, at store index firstChildIndex + ordinal once the store has grown far
    // enough to prove it exists.
    //
    // The parent is named by a discriminator rather than a ref param: pre-probe reads use a
    // copy (a pending grow resumes through a continuation that RE-ENTERS this method -- grows
    // are idempotent, and nothing mutates before the probe); the mutation re-acquires the frame
    // by ref after it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<bool> TryScheduleNextChildOf(bool fromVisitQueueFront)
    {
      var parent = fromVisitQueueFront ? _VisitQueue.GetFirst() : _ScheduleStack.GetLast();

      if (parent.ChildrenDisabled)
        return new ValueTask<bool>(false);

      var ordinal = parent.NextChildOrdinal;

      var available = _Store.EnsureChildAvailableAsync(parent.NodeIndex, ordinal);

      if (!available.IsCompletedSuccessfully)
        return AwaitThenTryScheduleNextChildOfAsync(available, fromVisitQueueFront);

      if (!available.Result)
        return new ValueTask<bool>(false);

      var childIndex = _Store.GetFirstChildIndex(parent.NodeIndex) + ordinal;

      ref var parentSlot = ref (fromVisitQueueFront ? ref _VisitQueue.GetFirst() : ref _ScheduleStack.GetLast());

      parentSlot.NextChildOrdinal++;

      PushScheduled(childIndex, new NodePosition(ordinal, parentSlot.Position.Depth + 1));

      return new ValueTask<bool>(true);
    }

    private ValueTask<bool> TryScheduleNextRoot()
    {
      if (_RootsFinished)
        return new ValueTask<bool>(false);

      var available = _Store.EnsureRootAvailableAsync(_RootsSeen);

      if (!available.IsCompletedSuccessfully)
        return AwaitThenTryScheduleNextRootAsync(available);

      if (!available.Result)
        return new ValueTask<bool>(false);

      // Roots are the depth-0 prefix: ordinal and store index coincide.
      PushScheduled(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

      return new ValueTask<bool>(true);
    }

    // codegen: begin async-only
    //
    // The suspension continuations. The grow continuations await the pending grow and RE-ENTER
    // the probing method (grows are idempotent and answer inline the second time); the schedule
    // continuations perform Advance's between-iteration mutation and re-enter Advance, which IS
    // the loop's `continue`.
    private async ValueTask<bool> AwaitThenTryScheduleNextChildOfAsync(ValueTask<bool> pendingGrow, bool fromVisitQueueFront)
    {
      await pendingGrow.ConfigureAwait(false);

      return await TryScheduleNextChildOf(fromVisitQueueFront).ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenTryScheduleNextRootAsync(ValueTask<bool> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await TryScheduleNextRoot().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitScheduleThenRetireStackTopAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _ScheduleStack.RemoveLast();

      return await Advance().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitScheduleThenFinishRootsAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _RootsScheduled = true;
      _SlotCarry = false;

      return await Advance().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitScheduleThenRetireFrontAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _VisitQueue.RemoveFirst();

      return await Advance().ConfigureAwait(false);
    }
    // codegen: end async-only

    // Schedule a node onto the schedule stack and publish its scheduling visit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushScheduled(int nodeIndex, NodePosition position)
    {
      _ScheduleStack.AddLast(new Frame
      {
        NodeIndex = nodeIndex,
        Position = position,
      });

      Mode = TreenumeratorMode.SchedulingNode;
      Node = _Store.GetValue(nodeIndex);
      VisitCount = 0;
      Position = position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishVisit(ref Frame frame)
    {
      Mode = TreenumeratorMode.VisitingNode;
      Node = _Store.GetValue(frame.NodeIndex);
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    // SkipSiblings: silence every frame that could still yield an effective sibling of the
    // just-scheduled node -- its skipped ancestors (the rest of the schedule stack), plus its
    // nearest accepted ancestor (the queue front). Returns true if the node was an effective
    // root, so the driver ends root enumeration.
    private bool SkipRemainingSiblings()
    {
      // Every schedule-stack frame except the node's own (the top) belongs to a skipped ancestor.
      for (int i = 1; i < _ScheduleStack.Count; i++)
        _ScheduleStack.GetFromBack(i).ChildrenDisabled = true;

      // The schedule stack holds only the node and its skipped ancestors, so Count - 1 is the
      // node's skipped-ancestor count. When that equals its depth every ancestor is skipped: the
      // node is an effective root, its siblings are the remaining roots. Otherwise its nearest
      // accepted ancestor is the queue front, whose remaining children we silence.
      if (_ScheduleStack.GetLast().Position.Depth == _ScheduleStack.Count - 1)
        return true;

      _VisitQueue.GetFirst().ChildrenDisabled = true;

      return false;
    }
  }
}
