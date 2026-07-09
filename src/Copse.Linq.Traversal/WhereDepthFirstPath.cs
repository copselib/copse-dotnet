using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// The root-to-current-node path of a depth-first <c>Where</c> traversal and all of its structural
  /// bookkeeping, isolated behind intention-revealing operations so the treenumerator that drives it
  /// stays a thin shell. Mirrors <see cref="Copse.Treenumerators.DepthFirstPath{TNode, TChildEnumerator}"/>
  /// for the base engine.
  ///
  /// <para><b>Sans-I/O.</b> The path NEVER touches the inner treenumerator. The two I/O concerns --
  /// pulling the next inner visit (<c>InnerTreenumerator.MoveNext</c>) and evaluating the predicate --
  /// stay in the driver. The driver reads the inner Mode/Node/Position once per step and PASSES them
  /// into the path operations as parameters; the path holds no reference to the inner treenumerator. A
  /// sync and a future async driver can therefore share this struct verbatim.</para>
  ///
  /// <para><b>Two-stack memory model.</b> Like the base DFT path, two deques are kept rather than one
  /// cohesive frame per node so a predicate-skipped node costs only its small frame, not a full
  /// accepted-node frame, and so depth compression / sibling renumbering fall out of the deque
  /// counts:</para>
  /// <list type="bullet">
  /// <item><b>_NodeVisits</b>: the visit-state of the ACCEPTED nodes on the path, plus a sentinel root
  /// at index 0 that gives every real node a parent to reference.</item>
  /// <item><b>_SkippedNodeVisits</b>: the predicate-skipped ancestors on the path, kept resident so
  /// their children promote into the nearest accepted ancestor's slot.</item>
  /// </list>
  /// The two diverge under filtering: a skipped node lives on <c>_SkippedNodeVisits</c> while the
  /// accepted path above it stays on <c>_NodeVisits</c>, so the deeper of the two tops
  /// (<see cref="DeepestFrame"/>) is the true current frame.
  ///
  /// <para><b>Layout.</b> A mutable struct embedded as a single non-readonly field of the driver, never
  /// copied -- only accessed as the driver's <c>_Path</c> field (so calls bind <c>ref this</c> and
  /// mutations persist). Every <c>ref</c> it returns points into the heap-allocated deques, never into
  /// the struct's own fields (the two int depth-trackers are read via int accessors and written only by
  /// <see cref="RecordPublished"/> / <see cref="PopDeeperThanForScheduling"/>).</para>
  /// </summary>
  internal struct WhereDepthFirstPath<TNode>
  {
    public WhereDepthFirstPath(TNode sentinelNode, NodePosition sentinelPosition)
    {
      _NodeVisits = new RefSemiDeque<InternalNodeVisit>();
      _SkippedNodeVisits = new RefSemiDeque<InternalNodeVisit>();

      // Seed the sentinel root: a virtual, already-visited parent above every real node so the path
      // always has a frame to reference when computing sibling indices and depths.
      _NodeVisits.AddLast(new InternalNodeVisit(sentinelNode, sentinelPosition, sentinelPosition, 1, 0));

      _DepthOfLastSeenNode = -1;
      _DepthOfLastVisitedNode = -1;
    }

    // Accepted nodes on the path (a sentinel root at index 0; predicate-skipped nodes are absent).
    private readonly RefSemiDeque<InternalNodeVisit> _NodeVisits;

    // Predicate-skipped ancestors on the path, kept resident to promote their children.
    private readonly RefSemiDeque<InternalNodeVisit> _SkippedNodeVisits;

    // Original (inner) depth of the most recently published scheduling/visiting node, and of the most
    // recently published VISITING node specifically. Single ints, so skipped nodes need no per-frame
    // visit bookkeeping; used to decide caching and redundant-visit suppression.
    private int _DepthOfLastSeenNode;
    private int _DepthOfLastVisitedNode;

    // The depth in the FILTERED tree = total path depth (accepted + skipped frames) minus the sentinel.
    public int EffectiveDepth
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _NodeVisits.Count + _SkippedNodeVisits.Count - 1;
    }

    // Number of accepted frames on the path, INCLUDING the sentinel (so the sentinel-only path is 1).
    public int AcceptedCount
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _NodeVisits.Count;
    }

    // The deepest frame on the path -- the top of whichever deque descends further in the inner tree.
    // Tie-break (verbatim from the original GetStackWithDeepestNodeVisit): _NodeVisits wins when there
    // are no skipped frames, or when its top is strictly deeper than the skipped top.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref InternalNodeVisit DeepestFrame() => ref DeepestDeque().GetLast();

    // True iff the deepest frame lives on the skipped deque (the negation of the tie-break above).
    public bool DeepestIsSkipped
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => DeepestDeque() == _SkippedNodeVisits;
    }

    // Whichever deque holds the deepest frame. Private so the deque never leaks to the driver -- the
    // driver only ever sees frames by ref. (Returns the deque reference itself, not a ref to the field,
    // so the fields stay readonly like the base DepthFirstPath.)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RefSemiDeque<InternalNodeVisit> DeepestDeque()
      => _SkippedNodeVisits.Count == 0
        || _NodeVisits.GetLast().OriginalPosition.Depth > _SkippedNodeVisits.GetLast().OriginalPosition.Depth
        ? _NodeVisits
        : _SkippedNodeVisits;

    // The accepted top -- the frame the driver publishes at the two publish sites (cached-child and
    // accept). This is the ACCEPTED node just pushed/cached, which is NOT necessarily the deepest frame
    // when a skipped ancestor sits below it, so the driver must use this, not DeepestFrame().
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref InternalNodeVisit AcceptedTop() => ref _NodeVisits.GetLast();

    // Consumer SkipNode: move the just-scheduled accepted node onto the skipped deque so its children
    // promote into the nearest accepted ancestor. The driver owns the guard (never the sentinel).
    public void MoveLastAcceptedToSkipped()
      => _SkippedNodeVisits.AddLast(_NodeVisits.RemoveLast());

    // Backtrack before scheduling the node at <paramref name="innerDepth"/>: pop every frame on the path
    // that is at or below that inner depth, then top up the visit count of the frame we returned to so
    // the next child's sibling index is computed correctly.
    public void PopDeeperThanForScheduling(int innerDepth)
    {
      // Descending further into the tree -- nothing to unwind.
      if (innerDepth > DeepestFrame().OriginalPosition.Depth)
        return;

      while (_SkippedNodeVisits.Count > 0
        && _SkippedNodeVisits.GetLast().OriginalPosition.Depth >= innerDepth)
        _SkippedNodeVisits.RemoveLast();

      while (_NodeVisits.GetLast().OriginalPosition.Depth >= innerDepth)
        _NodeVisits.RemoveLast();

      // Re-resolve the deepest frame after unwinding and bump its visit count so the sibling-index
      // counter (CurrentChildIndex, driven off VisitCount on the sentinel/skipped path) stays correct.
      if (DeepestIsSkipped)
        _SkippedNodeVisits.GetLast().VisitCount++;
      else if (_NodeVisits.Count == 1)
        _NodeVisits.GetLast().VisitCount++;
    }

    // Accept the just-scheduled node: assign it the next sibling index under its effective parent (the
    // deepest current frame), then push it as a new accepted frame. Returns its state for publishing.
    //
    // NOTE: reads the parent via DeepestFrame() PRE-push, so callers that also need the pre-push parent
    // (ShouldCacheChild) must run BEFORE this.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref InternalNodeVisit PushAcceptedChild(TNode node, NodePosition originalPosition)
    {
      ref var previous = ref DeepestFrame();

      var depth = EffectiveDepth;
      var siblingIndex = previous.CurrentChildIndex;

      previous.CurrentChildIndex++;

      _NodeVisits.AddLast(
        new InternalNodeVisit(node, originalPosition, new NodePosition(siblingIndex, depth), 0, 0));

      return ref _NodeVisits.GetLast();
    }

    // Whether the node about to be accepted should be cached (its first visit deferred): true when its
    // accepted parent is owed a between-children return visit before this child is published.
    //
    // MUST be called BEFORE PushAcceptedChild: it reads _NodeVisits.GetLast() as the PARENT (the current
    // accepted top), which PushAcceptedChild then replaces with the child.
    public bool ShouldCacheChild()
      => !DeepestIsSkipped
        && _NodeVisits.Count > 1
        && _DepthOfLastVisitedNode > _NodeVisits.GetLast().OriginalPosition.Depth;

    // Cached-child path: the accepted parent owes a return visit before the cached child is published.
    // Bump and return the parent (one below the just-pushed child) for the driver to publish.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref InternalNodeVisit TakeParentReturnVisit()
    {
      ref var parent = ref _NodeVisits.GetFromBack(1);
      parent.VisitCount++;
      return ref parent;
    }

    // Backtrack before visiting the node at <paramref name="innerDepth"/>: pop every frame strictly
    // deeper than it. Reports whether any popped accepted frame had already been visited and whether any
    // skipped frame was popped -- both feed the redundant-visit suppression in ShouldSuppressVisit.
    public void PopDeeperThanForVisiting(
      int innerDepth,
      out bool removedVisitedNodes,
      out bool removedSkippedNodes)
    {
      removedVisitedNodes = false;
      removedSkippedNodes = false;

      while (_NodeVisits.GetLast().OriginalPosition.Depth > innerDepth)
      {
        ref var removed = ref _NodeVisits.RemoveLast();
        removedVisitedNodes |= removed.VisitCount > 0;
      }

      while (_SkippedNodeVisits.Count > 0
        && _SkippedNodeVisits.GetLast().OriginalPosition.Depth > innerDepth)
      {
        _SkippedNodeVisits.RemoveLast();
        removedSkippedNodes = true;
      }
    }

    // Whether the inner visit at <paramref name="innerPosition"/> is redundant and should be suppressed
    // (true == suppress). A visit is suppressed when it would re-emit a node whose accepted frame is the
    // current top at the same original position but whose visit was already accounted for -- either a
    // skipped frame was just removed, or a deeper node has since been seen.
    public bool ShouldSuppressVisit(
      NodePosition innerPosition,
      bool removedVisitedNodes,
      bool removedSkippedNodes)
    {
      ref var n = ref _NodeVisits.GetLast();

      if (n.OriginalPosition == innerPosition
        && n.OriginalPosition.Depth >= _DepthOfLastVisitedNode
        && !removedVisitedNodes)
      {
        if (removedSkippedNodes)
          return true;

        if (_DepthOfLastSeenNode > n.OriginalPosition.Depth)
          return true;
      }

      return false;
    }

    // Emit the accepted top's next visit; returns its state for publishing.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref InternalNodeVisit TakeCurrentVisit()
    {
      ref var n = ref _NodeVisits.GetLast();
      n.VisitCount++;
      return ref n;
    }

    // Record that a frame was just published: update the last-seen depth, and -- if this was a visiting
    // node -- the last-visited depth. Driven by the driver after every publish.
    public void RecordPublished(int originalDepth, bool isVisiting)
    {
      _DepthOfLastSeenNode = originalDepth;

      if (isVisiting)
        _DepthOfLastVisitedNode = originalDepth;
    }

    // The visit-state of one node on the path. A plain value struct holding no enumerators (so the path
    // is not IDisposable). OriginalPosition is the inner/unfiltered position; Position is the compressed
    // filtered position published to the consumer.
    internal struct InternalNodeVisit
    {
      public InternalNodeVisit(
        TNode node,
        NodePosition originalPosition,
        NodePosition position,
        int visitCount,
        int currentChildIndex)
      {
        Node = node;
        OriginalPosition = originalPosition;
        Position = position;
        VisitCount = visitCount;
        CurrentChildIndex = currentChildIndex;
      }

      public TNode Node;
      public NodePosition OriginalPosition;
      public NodePosition Position;
      public int VisitCount;
      public int CurrentChildIndex;

      public override string ToString()
      {
        return $"N:{Node},  OP:{OriginalPosition},  P:{Position}  VC:{VisitCount},  CI:{CurrentChildIndex}";
      }
    }
  }
}
