using Copse.Core;
using Copse.Core.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// Breadth-first <b>async</b> treenumerator over a forward-only level-order stream, in the
  /// <b>direct style</b>: the same driver (schedule stack + visit queue + slot carry) over a sliding
  /// WINDOW of parsed entries, with <c>await</c> at the parse seam (ParseOneStep / AdvanceGroup).
  /// O(width) resident state.
  ///
  /// <para><b>This is the single source of truth.</b> Strip the <c>await</c>s and it collapses to the
  /// synchronous <c>Copse.Treenumerators.LevelOrderStreamBreadthFirstTreenumerator</c> (the checked-in
  /// <c>.g.cs</c> twin). Two async-legality restructurings vs a naive port: the ref-typed
  /// TryScheduleNextChildOf parameter became a bool discriminator (a ref param can't be in an async
  /// method) and phase 3's visit-front ref is scoped before the await (a ref local can't cross it).</para>
  /// </summary>
  public sealed class AsyncLevelOrderStreamBreadthFirstTreenumerator<TValue, TStream>
    : IAsyncTreenumerator<TValue>
    where TStream : IAsyncLevelOrderStream<TValue>
  {
    public AsyncLevelOrderStreamBreadthFirstTreenumerator(TStream stream)
    {
      _Stream = stream;
      _Window = new RefSemiDeque<Entry>();
      _VisitQueue = new RefSemiDeque<Frame>();
      _ScheduleStack = new RefSemiDeque<Frame>();
      _QueueIndexMins = new RefSemiDeque<int>();
    }

    // Non-readonly so interface calls on a struct stream mutate it in place rather than a
    // defensive copy.
    private TStream _Stream;

    // ----- The window: parsed stream nodes, absolute-indexed, evicted behind the visit front.

    private readonly RefSemiDeque<Entry> _Window;
    private int _WindowBase;      // absolute index of the window's first retained entry
    private int _AppendedCount;   // absolute index the next parsed entry will take

    // The node whose group the stream cursor is inside; -1 is the roots group (group 0 belongs
    // to the virtual forest). Advances by one per group -- the encoding's positional contract.
    private int _CurrentGroupOwner = -1;
    private int _RootCount;
    private bool _StreamExhausted;
    private bool _SuppressFutureRoots; // SkipSiblings on an effective root: discard the rest of group 0

    private struct Entry
    {
      public TValue Value;
      public int FirstChildIndex;   // absolute; -1 until the first child is appended
      public int ChildCount;
      public bool SuppressChildren; // pruned: when this entry's group arrives, discard-and-count
    }

    // ----- The driver: identical structure to the store twin.

    private readonly RefSemiDeque<Frame> _VisitQueue;
    private readonly RefSemiDeque<Frame> _ScheduleStack;

    // Sliding-window minimum of the visit queue's node indices. Queue indices are NOT
    // monotonic -- SkipNode promotion enqueues a deeper (higher-index) descendant ahead of the
    // front's later (lower-index) children -- so eviction must be bounded by the smallest LIVE
    // index, not the retired one. Standard monotonic-deque min: O(1) amortized.
    private readonly RefSemiDeque<int> _QueueIndexMins;

    private int _RootsSeen;
    private bool _RootsFinished;
    private bool _RootsScheduled;
    private bool _SlotCarry;

    private bool _Finished;

    public TValue Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    private struct Frame
    {
      public int NodeIndex;
      public NodePosition Position;
      public int VisitCount;
      public int NextChildOrdinal;
      public bool ChildrenDisabled;
    }

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      var moved = await OnMoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // A strategy only applies to the node just scheduled (an empty stack means we have not
      // scheduled anything yet -- the very first move). Visiting nodes ignore the strategy.
      if (Mode == TreenumeratorMode.SchedulingNode && _ScheduleStack.Count > 0)
        ApplyStrategy(nodeTraversalStrategies);

      return AdvanceAsync();
    }

    // Produce the next single visit (or false when the traversal is exhausted). See the store
    // twin for the phase structure.
    private async ValueTask<bool> AdvanceAsync()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the node on top of the schedule stack.
        if (_ScheduleStack.Count > 0)
        {
          if (await TryScheduleNextChildOfAsync(fromVisitFront: false).ConfigureAwait(false))
            return true;

          _ScheduleStack.RemoveLast();
          continue;
        }

        // 2) Schedule the next root (the forest's children -- no surrounding visits).
        if (!_RootsScheduled)
        {
          if (await TryScheduleNextRootAsync().ConfigureAwait(false))
            return true;

          _RootsScheduled = true;
          // Enqueues made while scheduling roots have no owing parent; clear the carry.
          _SlotCarry = false;
          continue;
        }

        if (_VisitQueue.Count == 0)
          return false;

        // 3) Visit the active parent and drive its children. The visit-front ref is scoped to this
        // block so it does not cross the TryScheduleNextChildOf await below (a ref local may not).
        {
          ref var front = ref _VisitQueue.GetFirst();

          if (front.VisitCount == 0)
          {
            front.VisitCount = 1;
            PublishVisit(ref front);
            return true;
          }

          if (_SlotCarry)
          {
            _SlotCarry = false;
            front.VisitCount++;
            PublishVisit(ref front);
            return true;
          }
        }

        if (await TryScheduleNextChildOfAsync(fromVisitFront: true).ConfigureAwait(false))
          return true;

        // The parent has no more children: retire it and evict the window behind everything
        // still live. Eviction is bounded by (a) the smallest node index still on the visit
        // queue (indices are non-monotonic under SkipNode promotion, so the retired index alone
        // proves nothing) and (b) the parse cursor -- a front whose children the consumer
        // disabled retires WITHOUT its group being reached, and its entry's suppress flag must
        // survive until the parser discards that group.
        var retired = _VisitQueue.RemoveFirst().NodeIndex;

        if (_QueueIndexMins.Count > 0 && _QueueIndexMins.GetFirst() == retired)
          _QueueIndexMins.RemoveFirst();

        var evictThrough = _VisitQueue.Count == 0 ? retired : _QueueIndexMins.GetFirst() - 1;

        if (!_StreamExhausted)
          evictThrough = System.Math.Min(evictThrough, _CurrentGroupOwner - 1);

        EvictThrough(evictThrough);
      }
    }

    // Classify the node just scheduled (the schedule-stack top) by the consumer's strategy.
    // Pruning marks the node's window entry too: its group must still be positionally consumed,
    // as a discard.
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
        // Erase the node and its subtree; the slot enqueues nothing, the group gets discarded.
        GetEntry(_ScheduleStack.GetLast().NodeIndex).SuppressChildren = true;
        _ScheduleStack.RemoveLast();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        // Keep the node resident so Advance can promote its children into its slot.
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
      {
        // Accept the node but give it no children, then fall through to the accept below.
        _ScheduleStack.GetLast().ChildrenDisabled = true;
        GetEntry(_ScheduleStack.GetLast().NodeIndex).SuppressChildren = true;
      }

      // Accept (TraverseAll, or the SkipDescendants fall-through): move the node onto the visit
      // queue, and record that this child slot enqueued an accepted node.
      var accepted = _ScheduleStack.RemoveLast();

      while (_QueueIndexMins.Count > 0 && _QueueIndexMins.GetLast() > accepted.NodeIndex)
        _QueueIndexMins.RemoveLast();
      _QueueIndexMins.AddLast(accepted.NodeIndex);

      _VisitQueue.AddLast(accepted);
      _SlotCarry = true;
    }

    // THE SEAM, windowed edition: the parent's next child is child ordinal NextChildOrdinal, once
    // the parser has advanced far enough to prove it exists. fromVisitFront selects the parent
    // frame (visit-queue front vs schedule-stack top) without a ref parameter (illegal in async):
    // read a value copy before the await, re-acquire the frame to bump NextChildOrdinal after it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<bool> TryScheduleNextChildOfAsync(bool fromVisitFront)
    {
      var parent = fromVisitFront ? _VisitQueue.GetFirst() : _ScheduleStack.GetLast();

      if (parent.ChildrenDisabled)
        return false;

      var ordinal = parent.NextChildOrdinal;
      var nodeIndex = parent.NodeIndex;
      var depth = parent.Position.Depth;

      if (!await TryEnsureChildAsync(nodeIndex, ordinal).ConfigureAwait(false))
        return false;

      var childIndex = GetEntry(nodeIndex).FirstChildIndex + ordinal;

      // The parse leaves the schedule stack / visit queue untouched, so the same frame is here.
      if (fromVisitFront)
        _VisitQueue.GetFirst().NextChildOrdinal++;
      else
        _ScheduleStack.GetLast().NextChildOrdinal++;

      PushScheduled(childIndex, new NodePosition(ordinal, depth + 1));

      return true;
    }

    private async ValueTask<bool> TryScheduleNextRootAsync()
    {
      if (_RootsFinished || !await TryEnsureRootAsync(_RootsSeen).ConfigureAwait(false))
        return false;

      // Roots are the entries of group 0: ordinal and absolute index coincide.
      PushScheduled(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

      return true;
    }

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
      Node = GetEntry(nodeIndex).Value;
      VisitCount = 0;
      Position = position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishVisit(ref Frame frame)
    {
      Mode = TreenumeratorMode.VisitingNode;
      Node = GetEntry(frame.NodeIndex).Value;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    // SkipSiblings: silence every frame that could still yield an effective sibling of the
    // just-scheduled node, marking the window entries so their groups' remainders get discarded.
    // Returns true if the node was an effective root, so the driver ends root enumeration (and
    // group 0's remainder is discarded when the parser next touches it).
    private bool SkipRemainingSiblings()
    {
      // Every schedule-stack frame except the node's own (the top) belongs to a skipped ancestor.
      for (int i = 1; i < _ScheduleStack.Count; i++)
      {
        ref var ancestor = ref _ScheduleStack.GetFromBack(i);

        ancestor.ChildrenDisabled = true;
        GetEntry(ancestor.NodeIndex).SuppressChildren = true;
      }

      // The schedule stack holds only the node and its skipped ancestors, so Count - 1 is the
      // node's skipped-ancestor count. When that equals its depth every ancestor is skipped: the
      // node is an effective root, its siblings are the remaining roots. Otherwise its nearest
      // accepted ancestor is the queue front, whose remaining children we silence.
      if (_ScheduleStack.GetLast().Position.Depth == _ScheduleStack.Count - 1)
      {
        _SuppressFutureRoots = true;
        return true;
      }

      ref var front = ref _VisitQueue.GetFirst();

      front.ChildrenDisabled = true;
      GetEntry(front.NodeIndex).SuppressChildren = true;

      return false;
    }

    // ----- The parser: one group at a time, positionally.

    private async ValueTask<bool> TryEnsureRootAsync(int k)
    {
      while (_RootCount <= k)
      {
        if (_StreamExhausted || _CurrentGroupOwner > -1)
          return false;

        await ParseOneStepAsync().ConfigureAwait(false);
      }

      return true;
    }

    private async ValueTask<bool> TryEnsureChildAsync(int parent, int k)
    {
      while (GetEntry(parent).ChildCount <= k)
      {
        // The parent's group is fully consumed once the cursor has moved past it (groups arrive
        // in owner order) or the stream ended (all unwritten groups are empty).
        if (_StreamExhausted || _CurrentGroupOwner > parent)
          return false;

        await ParseOneStepAsync().ConfigureAwait(false);
      }

      return true;
    }

    // Advance the parse by one item or one group boundary. A suppressed owner's group is
    // discarded UNMAPPED in one step, its count appended as suppressed entries (the cascade
    // that prunes whole subtrees out of a positional encoding).
    private async ValueTask ParseOneStepAsync()
    {
      var owner = _CurrentGroupOwner;

      var suppressed = owner == -1
        ? _SuppressFutureRoots
        : GetEntry(owner).SuppressChildren;

      if (suppressed)
      {
        var discarded = await _Stream.SkipGroupRemainderAsync().ConfigureAwait(false);

        for (int i = 0; i < discarded; i++)
          AppendEntry(default, suppressChildren: true, owner);

        await AdvanceGroupAsync().ConfigureAwait(false);

        return;
      }

      var read = await _Stream.TryReadNextInGroupAsync().ConfigureAwait(false);

      if (read.HasValue)
      {
        AppendEntry(read.Value, suppressChildren: false, owner);

        return;
      }

      await AdvanceGroupAsync().ConfigureAwait(false);
    }

    private void AppendEntry(TValue value, bool suppressChildren, int owner)
    {
      var index = _AppendedCount++;

      _Window.AddLast(new Entry
      {
        Value = value,
        FirstChildIndex = -1,
        SuppressChildren = suppressChildren,
      });

      if (owner == -1)
      {
        _RootCount++;
      }
      else
      {
        ref var ownerEntry = ref GetEntry(owner);

        if (ownerEntry.ChildCount == 0)
          ownerEntry.FirstChildIndex = index;

        ownerEntry.ChildCount++;
      }
    }

    private async ValueTask AdvanceGroupAsync()
    {
      if (await _Stream.TryMoveToNextGroupAsync().ConfigureAwait(false))
        _CurrentGroupOwner++;
      else
        _StreamExhausted = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref Entry GetEntry(int index)
      => ref _Window.GetFromBack(_Window.Count - 1 - (index - _WindowBase));

    private void EvictThrough(int index)
    {
      while (_WindowBase <= index)
      {
        _Window.RemoveFirst();
        _WindowBase++;
      }
    }

    public async ValueTask DisposeAsync()
    {
      await _Stream.DisposeAsync().ConfigureAwait(false);
    }
  }
}
