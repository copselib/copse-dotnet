using Copse.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// The accepted-node queue of a breadth-first <c>Where</c> traversal and all of its structural
  /// bookkeeping, isolated behind intention-revealing operations so the treenumerator that drives it
  /// stays a thin shell. Mirrors <see cref="WhereDepthFirstPath{TNode}"/> for the depth-first engine
  /// and <see cref="Copse.Treenumerators.BreadthFirstPath{TNode, TChildEnumerator}"/> for the base
  /// engine.
  ///
  /// <para><b>Sans-I/O.</b> The path NEVER touches the inner treenumerator. The two I/O concerns --
  /// pulling the next inner visit (<c>InnerTreenumerator.MoveNext</c>) and evaluating the predicate --
  /// stay in the driver, along with the output-sequencing cadence tokens. The driver reads the inner
  /// Mode/Position once per step and passes them into the path operations as parameters; the path holds
  /// no reference to the inner treenumerator, so a sync and a future async driver can share it verbatim.</para>
  ///
  /// <para>State splits into three orthogonal axes:</para>
  /// <list type="bullet">
  /// <item><b>The accepted queue</b> (<see cref="_AcceptedQueue"/>): the base engine's visit-queue
  /// analogue; the front is the active effective parent.</item>
  /// <item><b>The predicate-skipped-ancestor prefix carry</b> (regioned, off-limits): an O(1)
  /// effective-depth lookup with O(stored-skip-depth) memory.</item>
  /// <item><b>The consumer-SkipNode axis</b> (regioned, off-limits): orthogonal to predicate-skip;
  /// the consumer drops a predicate-accepted node.</item>
  /// </list>
  ///
  /// <para><b>Layout.</b> A mutable struct, embedded as the driver's single non-readonly <c>_Path</c>
  /// field, never copied -- only accessed as <c>_Path.Member</c> (so calls bind <c>ref this</c> and its
  /// mutations persist). Every <c>ref</c> it returns points into the heap-allocated queue, never into
  /// the struct's own scalar fields; those are read via accessors and mutated by void ops.</para>
  /// </summary>
  internal struct WhereBreadthFirstPath<TNode>
  {
    public WhereBreadthFirstPath(TNode sentinelNode, NodePosition sentinelPosition)
    {
      _AcceptedQueue = new RefSemiDeque<AcceptedFrame>();
      _RemovedSkippedChildCounts = new List<int>();
      _PrefixStored = new List<int>();
      _PrefixStoredCount = 0;
      _PrefixTail = 0;
      _RootNodesSeen = 0;
      _ConsumerSkippedSubtreeDepth = -1;
      _ConsumerSkippedChildAfterLastAccepted = false;

      // Seed the sentinel root: a virtual effective parent above every real node so the front is always
      // a valid frame to reference when computing sibling indices.
      _AcceptedQueue.AddLast(new AcceptedFrame(sentinelNode, sentinelPosition, 0, -1));
    }

    // Accepted nodes, scheduled but not yet fully visited. The front is the active effective parent.
    private readonly RefSemiDeque<AcceptedFrame> _AcceptedQueue;

    // Number of effective-root nodes seen so far -- the sibling index of the next effective root.
    // Read internally by GetEffectivePosition; bumped only inside SelectPublishFrame.
    private int _RootNodesSeen;

    // The active effective parent (the queue front) and the most recently scheduled node (the queue back).
    public ref AcceptedFrame Front
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _AcceptedQueue.GetFirst();
    }

    public ref AcceptedFrame Back
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _AcceptedQueue.GetLast();
    }

    // Enqueue a freshly accepted node at its compressed effective position.
    public void EnqueueAccepted(TNode node, NodePosition effectivePosition, int innerDepth)
      => _AcceptedQueue.AddLast(new AcceptedFrame(node, effectivePosition, 0, innerDepth));

    // Drop the most recently enqueued node (a consumer-SkipNode'd node removed before promotion).
    private void RemoveBack() => _AcceptedQueue.RemoveLast();

    // Retire the fully-visited front and re-establish the live invariants for the new front, ATOMICALLY
    // (the three steps must run in this exact order): drop the old front, reset the consumer-skip child
    // counts, then re-anchor the skipped-ancestor prefix at the new front. Returns the new front by ref
    // so the driver can bump its visit count without a second lookup.
    public ref AcceptedFrame RetireFrontAndReanchor()
    {
      _AcceptedQueue.RemoveFirst();
      ClearAllRemovedSkippedChildCounts();

      ref var front = ref _AcceptedQueue.GetFirst();
      if (front.InnerDepth >= 0)
        PrefixAnchor(front.InnerDepth, front.InnerDepth - front.Position.Depth);

      return ref front;
    }

    // Record the just-accepted child under its effective parent: bump the consumer-skipped parent's
    // running child count if this falls under one, else the front's accepted-child count (the source of
    // the next sibling's index). Effective roots (depth 0) have no parent to credit.
    public void RecordAcceptedChild(NodePosition effectivePosition)
    {
      if (effectivePosition.Depth <= 0)
        return;

      var parentDepth = effectivePosition.Depth - 1;
      if (GetRemovedSkippedChildCount(parentDepth) >= 0)
        IncrementRemovedSkippedChildCount(parentDepth);
      else
        Front.AcceptedChildCount++;
    }

    // The frame the driver publishes for an inner visit: the just-scheduled back when scheduling, else
    // the front being visited. Counting a freshly seen effective root rides here with frame selection
    // (it bumps only _RootNodesSeen, never the published frame).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref AcceptedFrame SelectPublishFrame(bool isScheduling)
    {
      ref var frame = ref isScheduling ? ref Back : ref Front;

      if (isScheduling && frame.Position.Depth == 0)
        _RootNodesSeen++;

      return ref frame;
    }

    // The compressed effective position of the just-scheduled accepted node at inner depth
    // <paramref name="innerDepth"/>: effective depth is inner depth minus the predicate-skipped-ancestor
    // count; the sibling index comes from the effective root count, a removed consumer-skip parent's
    // running count, or the front's accepted-child count.
    public NodePosition GetEffectivePosition(int innerDepth, out bool immediateParentIsSkipped)
    {
      var skippedAncestorCount = PrefixSkippedAncestorCount(innerDepth, out immediateParentIsSkipped);
      var effectiveDepth = innerDepth - skippedAncestorCount;

      int effectiveSiblingIndex;

      if (effectiveDepth == 0)
      {
        effectiveSiblingIndex = _RootNodesSeen;
      }
      else
      {
        // Check if this is a child of a recently removed consumer-SkipNode'd parent
        var parentDepth = effectiveDepth - 1;
        var removedChildCount = GetRemovedSkippedChildCount(parentDepth);
        if (removedChildCount >= 0)
        {
          effectiveSiblingIndex = removedChildCount;
        }
        else
        {
          effectiveSiblingIndex = Front.AcceptedChildCount;
        }
      }

      return new NodePosition(effectiveSiblingIndex, effectiveDepth);
    }

    // Update the live skipped-ancestor prefix for the node just scheduled at inner depth
    // <paramref name="innerDepth"/>: its prefix is the parent's plus one iff it was predicate-skipped.
    public void PrefixWriteForScheduledNode(int innerDepth, bool predicateSkipped)
      => PrefixWriteScheduled(innerDepth, (innerDepth > 0 ? PrefixRead(innerDepth - 1) : 0) + (predicateSkipped ? 1 : 0));

    #region Off-limits: predicate-skipped-ancestor prefix carry (f7eae61 O(N) time, ca567e0 O(1)-depth memory)

    // Incremental predicate-skipped-ancestor prefix: PrefixRead(d) = number of
    // PREDICATE-skipped nodes among inner depths 0..d on the current path. (Consumer-
    // SkipNode'd nodes are predicate-accepted, so they do NOT count here -- they are
    // handled by _RemovedSkippedChildCounts.) This gives an O(1) skipped-ancestor lookup
    // with no per-node stack scan. It is maintained incrementally: each scheduled node sets
    // its own depth (live), and when the queue front advances the prefix is re-anchored at
    // the new front's depth in O(1) (PrefixAnchor). Because the inner schedules depths
    // contiguously from the front downward, deeper entries are always rewritten before they
    // are read, so no full-path copy/restore is needed.
    //
    // STORAGE (tail-carry): the prefix is monotonic non-decreasing in depth and CONSTANT
    // beyond the deepest predicate-skipped ancestor on the current path (accepted nodes add 0).
    // So we store explicit per-depth entries only up to the deepest depth that differs from the
    // running tail value; reads past _PrefixStoredCount return _PrefixTail. With no predicate
    // skips (WhereAll) the stored region stays empty -- memory is O(stored skip depth), not
    // O(inner depth). This is the only thing that differs from a flat List<int> indexed by
    // absolute depth; the logical values are identical (verified value-by-value against the old
    // flat-list logic across the full Where2InProcessScan via a temporary validate-alongside shadow).
    private readonly List<int> _PrefixStored;
    private int _PrefixStoredCount;
    private int _PrefixTail;

    private int PrefixRead(int depth)
    {
      if (depth < 0)
        return 0;
      if (depth < _PrefixStoredCount)
        return _PrefixStored[depth];
      return _PrefixTail;
    }

    // Set the prefix at inner depth `depth` to `value` for the current scheduled node.
    // Only materializes stored entries when `value` exceeds the constant tail (i.e. a
    // predicate skip pushed the running count above the tail); equal-to-tail writes in the
    // accepted region are no-ops and allocate nothing.
    private void PrefixWriteScheduled(int depth, int value)
    {
      if (depth < _PrefixStoredCount)
      {
        _PrefixStored[depth] = value;
        return;
      }

      if (value == _PrefixTail)
        return; // still on the constant tail -- nothing to store.

      // A skip raised the count above the tail. Materialize [_PrefixStoredCount .. depth]:
      // the gap entries are still on the (old) tail, then this depth gets `value`.
      while (_PrefixStoredCount < depth)
      {
        if (_PrefixStoredCount < _PrefixStored.Count)
          _PrefixStored[_PrefixStoredCount] = _PrefixTail;
        else
          _PrefixStored.Add(_PrefixTail);
        _PrefixStoredCount++;
      }
      if (_PrefixStoredCount < _PrefixStored.Count)
        _PrefixStored[_PrefixStoredCount] = value;
      else
        _PrefixStored.Add(value);
      _PrefixStoredCount++;
    }

    private int PrefixSkippedAncestorCount(int innerDepth, out bool immediateParentIsSkipped)
    {
      var depth = innerDepth;
      if (depth == 0)
      {
        immediateParentIsSkipped = false;
        return 0;
      }
      var parentPrefix = PrefixRead(depth - 1);
      var grandparentPrefix = depth > 1 ? PrefixRead(depth - 2) : 0;
      immediateParentIsSkipped = parentPrefix > grandparentPrefix;
      return parentPrefix;
    }

    // Re-anchor the live skipped-ancestor prefix at the new queue front in O(1). The front
    // is an accepted (predicate-kept) node, so it contributes 0 to the prefix; hence
    // prefix[frontDepth] == prefix[frontDepth-1] == frontSkipPrefix, and EVERYTHING below the
    // front on the go-forward path is the constant frontSkipPrefix until the next skip. So we
    // make frontSkipPrefix the tail and drop stored entries at/above frontDepth-1: deeper slots
    // are re-materialized live (PrefixWriteScheduled) as the inner schedules the front's
    // descendants contiguously downward. Ancestor entries above the front (depths <
    // frontDepth-1, whose counts may be smaller) stay stored. This is what reclaims the memory
    // the old absolute-depth List<int> never released. Called ONLY by RetireFrontAndReanchor.
    private void PrefixAnchor(int frontInnerDepth, int frontSkipPrefix)
    {
      var keep = frontInnerDepth > 0 ? frontInnerDepth - 1 : 0;
      if (_PrefixStoredCount > keep)
        _PrefixStoredCount = keep;
      _PrefixTail = frontSkipPrefix;
    }

    #endregion Off-limits: predicate-skipped-ancestor prefix carry

    #region Off-limits: consumer-SkipNode axis (orthogonal to predicate-skip promotion)

    // When consumer-SkipNode'd nodes are removed from the queue, track child counts
    // per depth for sibling index calculation. Index = effective depth of the removed
    // parent; value = number of accepted children scheduled so far. -1 = no removed
    // parent at that depth. Supports nested consumer-SkipNode'd parents (e.g., both
    // a at depth 0 and b at depth 1 are SkipNode'd).
    private readonly List<int> _RemovedSkippedChildCounts;

    private int GetRemovedSkippedChildCount(int depth)
    {
      if (depth < 0 || depth >= _RemovedSkippedChildCounts.Count)
        return -1;
      return _RemovedSkippedChildCounts[depth];
    }

    private void SetRemovedSkippedChildCount(int depth, int count)
    {
      while (_RemovedSkippedChildCounts.Count <= depth)
        _RemovedSkippedChildCounts.Add(-1);
      _RemovedSkippedChildCounts[depth] = count;
    }

    private void IncrementRemovedSkippedChildCount(int depth)
    {
      _RemovedSkippedChildCounts[depth]++;
    }

    private void ClearAllRemovedSkippedChildCounts()
    {
      for (int i = 0; i < _RemovedSkippedChildCounts.Count; i++)
        _RemovedSkippedChildCounts[i] = -1;
    }

    // Remove the just-scheduled consumer-SkipNode'd node from the queue and record it at its effective
    // depth so its children and same-depth siblings get correct sibling indices.
    public void BeginRemovedSkippedParent(int effectiveDepth)
    {
      SetRemovedSkippedChildCount(effectiveDepth, 0);
      RemoveBack();
    }

    // Effective depth of a consumer-SkipNode'd node whose removal requires the wrapper to generate a
    // parent visit when scheduling exits the subtree. -1 when inactive, DeferredSchedulePending when a
    // deferred schedule is queued.
    private int _ConsumerSkippedSubtreeDepth;
    private const int DeferredSchedulePending = int.MinValue;

    // Whether the last child in a consumer-SkipNode'd parent's subtree was itself consumer-SkipNode'd.
    // Suppresses the deferred parent visit when true.
    private bool _ConsumerSkippedChildAfterLastAccepted;

    public int ConsumerSkippedSubtreeDepth => _ConsumerSkippedSubtreeDepth;
    public bool ConsumerSkipDeferredSchedulePending => _ConsumerSkippedSubtreeDepth == DeferredSchedulePending;
    public void ClearConsumerSkippedSubtree() => _ConsumerSkippedSubtreeDepth = -1;
    public void EnterConsumerSkippedSubtree(int effectiveDepth) => _ConsumerSkippedSubtreeDepth = effectiveDepth;
    public void MarkDeferredSchedulePending() => _ConsumerSkippedSubtreeDepth = DeferredSchedulePending;

    public bool ConsumerSkippedChildAfterLastAccepted => _ConsumerSkippedChildAfterLastAccepted;
    public void MarkConsumerSkippedChildAfterLastAccepted() => _ConsumerSkippedChildAfterLastAccepted = true;
    public void ClearConsumerSkippedChildAfterLastAccepted() => _ConsumerSkippedChildAfterLastAccepted = false;

    #endregion Off-limits: consumer-SkipNode axis

    // The visit-state of one accepted node on the queue. A plain value struct holding no enumerators (so
    // the path is not IDisposable). Position is the compressed filtered position published to the
    // consumer; InnerDepth is the original inner depth (-1 for the sentinel), used to re-anchor the
    // skipped-ancestor prefix when this node retires from the front.
    internal struct AcceptedFrame
    {
      public AcceptedFrame(
        TNode node,
        NodePosition position,
        int visitCount,
        int innerDepth,
        NodeTraversalStrategies nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll)
      {
        Node = node;
        Position = position;
        VisitCount = visitCount;
        InnerDepth = innerDepth;
        TraversalStrategy = nodeTraversalStrategies;
        AcceptedChildCount = 0;
      }

      public TNode Node { get; }
      public NodePosition Position { get; }
      public int VisitCount { get; set; }
      public int InnerDepth { get; }
      public NodeTraversalStrategies TraversalStrategy { get; set; }
      public bool Skipped => TraversalStrategy != NodeTraversalStrategies.TraverseAll;
      public int AcceptedChildCount { get; set; }

      public override string ToString() => $"({Position}), {VisitCount}, {TraversalStrategy}";
    }
  }
}
