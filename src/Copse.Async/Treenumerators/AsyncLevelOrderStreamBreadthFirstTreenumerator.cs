using Copse.Core;
using Copse.Core.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
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
  ///
  /// <para><b>Locality:</b> visits publish from the FRAME (the value is carried out of the window
  /// once, at schedule time) and the current group's owner span accumulates in mirror fields
  /// flushed once per group boundary -- the per-visit and per-item paths touch no window entry.
  /// The window is O(width) (megabytes at the Mega tier), and per-item random access into it was
  /// the dominant cost the FlatDecode family measured before those two moves.</para>
  ///
  /// <para><b>The window is a masked ring, not a deque:</b> it is a contiguous ABSOLUTE-index
  /// range [windowBase, appendedCount), so a power-of-two ring serves GetEntry with one masked
  /// array index and eviction is a base bump. A chunked deque's random access resolves its
  /// partition by WALKING the partition chain -- profiled at 87% of the whole Mega-Binary drain
  /// (RefSemiDeque.GetPartitionAndOffset), the FlatDecode family's 29x -- because only head/tail
  /// access has an O(1) path there. Ring growth re-lays live entries by their absolute index;
  /// evicted slots are not cleared (they are overwritten on wrap, so a reference-typed value can
  /// linger for up to one capacity's worth of appends -- bounded, and the deque kept chunks
  /// resident the same way).</para>
  /// </summary>
  public sealed class AsyncLevelOrderStreamBreadthFirstTreenumerator<TValue, TStream>
    : IAsyncTreenumerator<TValue>
    where TStream : IAsyncLevelOrderStream<TValue>
  {
    public AsyncLevelOrderStreamBreadthFirstTreenumerator(TStream stream)
    {
      _Stream = stream;
      _Window = new Entry[InitialWindowCapacity];
      _WindowMask = InitialWindowCapacity - 1;
      _VisitQueue = new RefSemiDeque<Frame>();
      _ScheduleStack = new RefSemiDeque<Frame>();
      _QueueIndexMins = new RefSemiDeque<int>();
    }

    // Non-readonly so interface calls on a struct stream mutate it in place rather than a
    // defensive copy.
    private TStream _Stream;

    // ----- The window: parsed stream nodes, absolute-indexed, evicted behind the visit front,
    // stored in a power-of-two ring keyed by absolute index & mask (see the class comment).

    private const int InitialWindowCapacity = 64;

    private Entry[] _Window;
    private int _WindowMask;
    private int _WindowBase;      // absolute index of the window's first retained entry
    private int _AppendedCount;   // absolute index the next parsed entry will take

    // The node whose group the stream cursor is inside; -1 is the roots group (group 0 belongs
    // to the virtual forest). Advances by one per group -- the encoding's positional contract.
    private int _CurrentGroupOwner = -1;
    private int _RootCount;
    private bool _StreamExhausted;
    private bool _SuppressFutureRoots; // SkipSiblings on an effective root: discard the rest of group 0

    // The CURRENT group's owner state, mirrored into fields so the per-item parse path touches
    // no window entry: the suppression flag loads when the group opens, the child span
    // accumulates here, and one flush writes it back at the group boundary -- one window touch
    // per GROUP instead of two or three per item. Kept exact mid-group: the one strategy that
    // can suppress the owner of the group the cursor is inside (SkipSiblings silencing the
    // visit front) updates the mirror too.
    private bool _CurrentGroupSuppressed;
    private int _CurrentGroupFirstChildIndex = -1;
    private int _CurrentGroupChildCount;

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
      public TValue Node; // carried from the window at schedule time: visits publish from the
                          // frame and never re-touch the window
      public NodePosition Position;
      public int VisitCount;
      public int NextChildOrdinal;
      public bool ChildrenDisabled;
    }

    // NOT async, and neither are the pull helpers below: every seam is PROBED, and a pull whose
    // stream answers inline is ordinary method calls with no state machine. Only a genuinely
    // pending stream call enters an async continuation -- the fast-path probe idiom (see
    // AsyncToSync).
    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return new ValueTask<bool>(false);

      var moved = OnMoveNextAsync(nodeTraversalStrategies);

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenFinishMoveNextAsync(moved);

      if (!moved.Result)
        _Finished = true;

      return new ValueTask<bool>(moved.Result);
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
    // twin for the phase structure. A pending schedule resumes through a continuation that
    // performs this loop's between-iteration mutation and RE-ENTERS Advance -- re-entry IS the
    // `continue` (all loop state lives in fields).
    private ValueTask<bool> AdvanceAsync()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the node on top of the schedule stack.
        if (_ScheduleStack.Count > 0)
        {
          var scheduled = TryScheduleNextChildOfAsync(fromVisitFront: false);

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
          var scheduled = TryScheduleNextRootAsync();

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

        // 3) Visit the active parent and drive its children. The visit-front ref is scoped to
        // this block so it does not cross the schedule call below.
        {
          ref var front = ref _VisitQueue.GetFirst();

          if (front.VisitCount == 0)
          {
            front.VisitCount = 1;
            PublishVisit(ref front);
            return new ValueTask<bool>(true);
          }

          if (_SlotCarry)
          {
            _SlotCarry = false;
            front.VisitCount++;
            PublishVisit(ref front);
            return new ValueTask<bool>(true);
          }
        }

        var frontScheduled = TryScheduleNextChildOfAsync(fromVisitFront: true);

        if (!frontScheduled.IsCompletedSuccessfully)
          return AwaitScheduleThenRetireFrontAsync(frontScheduled);

        if (frontScheduled.Result)
          return new ValueTask<bool>(true);

        RetireFront();
      }
    }

    // The parent has no more children: retire it and evict the window behind everything still
    // live. Eviction is bounded by (a) the smallest node index still on the visit queue
    // (indices are non-monotonic under SkipNode promotion, so the retired index alone proves
    // nothing) and (b) the parse cursor -- a front whose children the consumer disabled retires
    // WITHOUT its group being reached, and its entry's suppress flag must survive until the
    // parser discards that group.
    private void RetireFront()
    {
      var retired = _VisitQueue.RemoveFirst().NodeIndex;

      if (_QueueIndexMins.Count > 0 && _QueueIndexMins.GetFirst() == retired)
        _QueueIndexMins.RemoveFirst();

      var evictThrough = _VisitQueue.Count == 0 ? retired : _QueueIndexMins.GetFirst() - 1;

      if (!_StreamExhausted)
        evictThrough = System.Math.Min(evictThrough, _CurrentGroupOwner - 1);

      EvictThrough(evictThrough);
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
        // (No suppression-mirror update here or in the SkipDescendants arm: the cursor cannot
        // be inside a just-scheduled node's group -- its own group sits positionally AFTER the
        // group it was read from.)
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
    // frame (visit-queue front vs schedule-stack top) without a ref parameter: pre-probe reads
    // use a value copy (a pending ensure resumes through a continuation that RE-ENTERS this
    // method -- the ensure is idempotent, its parse lands in fields, and nothing mutates before
    // it), and the frame is re-acquired to bump NextChildOrdinal after.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<bool> TryScheduleNextChildOfAsync(bool fromVisitFront)
    {
      var parent = fromVisitFront ? _VisitQueue.GetFirst() : _ScheduleStack.GetLast();

      if (parent.ChildrenDisabled)
        return new ValueTask<bool>(false);

      var ordinal = parent.NextChildOrdinal;
      var nodeIndex = parent.NodeIndex;
      var depth = parent.Position.Depth;

      var ensured = TryEnsureChildAsync(nodeIndex, ordinal);

      if (!ensured.IsCompletedSuccessfully)
        return AwaitThenTryScheduleNextChildOfAsync(ensured, fromVisitFront);

      if (!ensured.Result)
        return new ValueTask<bool>(false);

      var childIndex = GetFirstChildIndex(nodeIndex) + ordinal;

      // The parse leaves the schedule stack / visit queue untouched, so the same frame is here.
      if (fromVisitFront)
        _VisitQueue.GetFirst().NextChildOrdinal++;
      else
        _ScheduleStack.GetLast().NextChildOrdinal++;

      PushScheduled(childIndex, new NodePosition(ordinal, depth + 1));

      return new ValueTask<bool>(true);
    }

    private ValueTask<bool> TryScheduleNextRootAsync()
    {
      if (_RootsFinished)
        return new ValueTask<bool>(false);

      var ensured = TryEnsureRootAsync(_RootsSeen);

      if (!ensured.IsCompletedSuccessfully)
        return AwaitThenTryScheduleNextRootAsync(ensured);

      if (!ensured.Result)
        return new ValueTask<bool>(false);

      // Roots are the entries of group 0: ordinal and absolute index coincide.
      PushScheduled(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

      return new ValueTask<bool>(true);
    }

    // Schedule a node onto the schedule stack and publish its scheduling visit. The value is
    // read from the window ONCE, here, and carried in the frame from then on.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushScheduled(int nodeIndex, NodePosition position)
    {
      var node = GetEntry(nodeIndex).Value;

      _ScheduleStack.AddLast(new Frame
      {
        NodeIndex = nodeIndex,
        Node = node,
        Position = position,
      });

      Mode = TreenumeratorMode.SchedulingNode;
      Node = node;
      VisitCount = 0;
      Position = position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishVisit(ref Frame frame)
    {
      Mode = TreenumeratorMode.VisitingNode;
      Node = frame.Node;
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

        if (_CurrentGroupOwner == -1)
          _CurrentGroupSuppressed = true; // the cursor may still be inside the roots group

        return true;
      }

      ref var front = ref _VisitQueue.GetFirst();

      front.ChildrenDisabled = true;
      GetEntry(front.NodeIndex).SuppressChildren = true;

      if (front.NodeIndex == _CurrentGroupOwner)
        _CurrentGroupSuppressed = true; // the cursor may be inside the front's own group

      return false;
    }

    // ----- The parser: one group at a time, positionally.

    private ValueTask<bool> TryEnsureRootAsync(int k)
    {
      while (_RootCount <= k)
      {
        if (_StreamExhausted || _CurrentGroupOwner > -1)
          return new ValueTask<bool>(false);

        var step = ParseOneStepAsync();

        if (!step.IsCompletedSuccessfully)
          return AwaitStepThenEnsureRootAsync(step, k);
      }

      return new ValueTask<bool>(true);
    }

    private ValueTask<bool> TryEnsureChildAsync(int parent, int k)
    {
      while (GetChildCount(parent) <= k)
      {
        // The parent's group is fully consumed once the cursor has moved past it (groups arrive
        // in owner order) or the stream ended (all unwritten groups are empty).
        if (_StreamExhausted || _CurrentGroupOwner > parent)
          return new ValueTask<bool>(false);

        var step = ParseOneStepAsync();

        if (!step.IsCompletedSuccessfully)
          return AwaitStepThenEnsureChildAsync(step, parent, k);
      }

      return new ValueTask<bool>(true);
    }

    // Live owner-aware reads: the current group's span lives in the mirror fields until the
    // boundary flush, so a parent whose group the cursor is inside reads from the fields and
    // everyone else from the window.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetChildCount(int parent)
      => parent == _CurrentGroupOwner ? _CurrentGroupChildCount : GetEntry(parent).ChildCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetFirstChildIndex(int parent)
      => parent == _CurrentGroupOwner ? _CurrentGroupFirstChildIndex : GetEntry(parent).FirstChildIndex;

    // Advance the parse by one item or one group boundary. A suppressed owner's group is
    // discarded UNMAPPED in one step, its count appended as suppressed entries (the cascade
    // that prunes whole subtrees out of a positional encoding). Returns true iff an item was
    // appended (false at a group boundary or exhaustion) -- the ensure loops re-check their own
    // conditions rather than reading it, but a bool-shaped step is what lets every seam here
    // ride the generic probe machinery.
    private ValueTask<bool> ParseOneStepAsync()
    {
      if (_CurrentGroupSuppressed)
      {
        var skipped = _Stream.SkipGroupRemainderAsync();

        if (!skipped.IsCompletedSuccessfully)
          return AwaitSkipThenFinishSuppressedGroupAsync(skipped);

        return FinishSuppressedGroupAsync(skipped.Result);
      }

      var read = _Stream.TryReadNextInGroupAsync();

      if (!read.IsCompletedSuccessfully)
        return AwaitReadThenAppendOrAdvanceAsync(read);

      if (read.Result.HasValue)
      {
        AppendEntry(read.Result.Value, suppressChildren: false);

        return new ValueTask<bool>(true);
      }

      return AdvanceGroupAsync();
    }

    private ValueTask<bool> FinishSuppressedGroupAsync(int discarded)
    {
      for (int i = 0; i < discarded; i++)
        AppendEntry(default, suppressChildren: true);

      return AdvanceGroupAsync();
    }

    private void AppendEntry(TValue value, bool suppressChildren)
    {
      EnsureWindowSlot();

      var index = _AppendedCount++;

      _Window[index & _WindowMask] = new Entry
      {
        Value = value,
        FirstChildIndex = -1,
        SuppressChildren = suppressChildren,
      };

      if (_CurrentGroupOwner == -1)
      {
        _RootCount++;
        return;
      }

      if (_CurrentGroupChildCount == 0)
        _CurrentGroupFirstChildIndex = index;

      _CurrentGroupChildCount++;
    }

    // Group boundary: flush the mirrored span to the owner's entry (the one window touch the
    // group pays), then advance and open the next group's mirror. Exhaustion still flushes --
    // the final group's span must land, and the flush is safe before the probe because a
    // pending advance resumes through a continuation that only OPENS (never re-flushes).
    // Returns true iff a new group opened (false = exhausted).
    private ValueTask<bool> AdvanceGroupAsync()
    {
      FlushCurrentGroup();

      var advanced = _Stream.TryMoveToNextGroupAsync();

      if (!advanced.IsCompletedSuccessfully)
        return AwaitThenOpenNextGroupAsync(advanced);

      return new ValueTask<bool>(OpenNextGroup(advanced.Result));
    }

    private bool OpenNextGroup(bool advanced)
    {
      if (advanced)
      {
        _CurrentGroupOwner++;
        OpenCurrentGroup();

        return true;
      }

      _StreamExhausted = true;

      return false;
    }

    private void FlushCurrentGroup()
    {
      if (_CurrentGroupOwner == -1)
        return; // roots count straight into _RootCount

      ref var owner = ref GetEntry(_CurrentGroupOwner);

      owner.FirstChildIndex = _CurrentGroupFirstChildIndex;
      owner.ChildCount = _CurrentGroupChildCount;
    }

    private void OpenCurrentGroup()
    {
      _CurrentGroupFirstChildIndex = -1;
      _CurrentGroupChildCount = 0;
      _CurrentGroupSuppressed = GetEntry(_CurrentGroupOwner).SuppressChildren;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref Entry GetEntry(int index)
      => ref _Window[index & _WindowMask];

    // Grow when the live range would overflow the ring: double and re-lay the live entries by
    // their absolute index (each stays addressable under the new mask). Amortized O(1) per
    // append; the ring settles at the tree's width rounded up to a power of two.
    private void EnsureWindowSlot()
    {
      var capacity = _WindowMask + 1;

      if (_AppendedCount - _WindowBase < capacity)
        return;

      var grown = new Entry[capacity * 2];
      var grownMask = grown.Length - 1;

      for (var index = _WindowBase; index < _AppendedCount; index++)
        grown[index & grownMask] = _Window[index & _WindowMask];

      _Window = grown;
      _WindowMask = grownMask;
    }

    private void EvictThrough(int index)
    {
      if (index >= _WindowBase)
        _WindowBase = index + 1;
    }

    // codegen: begin async-only
    //
    // The suspension continuations. Grow/ensure continuations await the pending step and
    // RE-ENTER their probing method (ensures are idempotent, the parse lands in fields, and the
    // probing methods mutate nothing before their probes); the read/skip continuations consume
    // the pending stream result and run the same tail the fast path would have; the schedule
    // continuations perform Advance's between-iteration mutation and re-enter Advance, which IS
    // the loop's `continue`.
    private async ValueTask<bool> AwaitThenFinishMoveNextAsync(ValueTask<bool> pendingMove)
    {
      var moved = await pendingMove.ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private async ValueTask<bool> AwaitScheduleThenRetireStackTopAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _ScheduleStack.RemoveLast();

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitScheduleThenFinishRootsAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      _RootsScheduled = true;
      _SlotCarry = false;

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitScheduleThenRetireFrontAsync(ValueTask<bool> pendingSchedule)
    {
      if (await pendingSchedule.ConfigureAwait(false))
        return true;

      RetireFront();

      return await AdvanceAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenTryScheduleNextChildOfAsync(ValueTask<bool> pendingEnsure, bool fromVisitFront)
    {
      await pendingEnsure.ConfigureAwait(false);

      return await TryScheduleNextChildOfAsync(fromVisitFront).ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenTryScheduleNextRootAsync(ValueTask<bool> pendingEnsure)
    {
      await pendingEnsure.ConfigureAwait(false);

      return await TryScheduleNextRootAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitStepThenEnsureRootAsync(ValueTask<bool> pendingStep, int k)
    {
      await pendingStep.ConfigureAwait(false);

      return await TryEnsureRootAsync(k).ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitStepThenEnsureChildAsync(ValueTask<bool> pendingStep, int parent, int k)
    {
      await pendingStep.ConfigureAwait(false);

      return await TryEnsureChildAsync(parent, k).ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitSkipThenFinishSuppressedGroupAsync(ValueTask<int> pendingSkip)
    {
      var discarded = await pendingSkip.ConfigureAwait(false);

      return await FinishSuppressedGroupAsync(discarded).ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenOpenNextGroupAsync(ValueTask<bool> pendingAdvance)
      => OpenNextGroup(await pendingAdvance.ConfigureAwait(false));

    private async ValueTask<bool> AwaitReadThenAppendOrAdvanceAsync(ValueTask<LevelOrderRead<TValue>> pendingRead)
    {
      var read = await pendingRead.ConfigureAwait(false);

      if (read.HasValue)
      {
        AppendEntry(read.Value, suppressChildren: false);

        return true;
      }

      return await AdvanceGroupAsync().ConfigureAwait(false);
    }
    // codegen: end async-only

    public async ValueTask DisposeAsync()
    {
      await _Stream.DisposeAsync().ConfigureAwait(false);
    }
  }
}
