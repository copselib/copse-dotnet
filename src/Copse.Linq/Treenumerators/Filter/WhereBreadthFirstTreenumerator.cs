using Copse.Core;
using Copse.Linq.Extensions;
using System;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// Breadth-first <c>Where</c>: filters the inner stream, promoting a predicate-skipped node's
  /// children into its slot, and re-emits the SAME visit multiset the base engine would for the
  /// filtered tree -- in level order. Predicate polarity is the LINQ convention (true = keep),
  /// matching <see cref="WhereDepthFirstTreenumerator{TNode}"/>; operators with removal semantics
  /// (PruneBefore) invert at their own call sites.
  ///
  /// <para>All structural state lives in <see cref="WhereBreadthFirstPath{TNode}"/>; this class is a
  /// thin driver over it. The only operations that touch the source are the two I/O seams pulling the
  /// next inner visit (<see cref="TreenumeratorWrapper{TNode}.InnerTreenumerator"/>.MoveNext) and
  /// evaluating <see cref="_Predicate"/>. The driver also owns the output-sequencing cadence tokens
  /// against the inner stream -- the front parent's return-visit token (<see cref="_FrontReturnVisit"/>,
  /// the wrapper's analogue of the base engine's one-bit <c>FrontSlotEnqueuedNode</c>, widened to three
  /// states because the wrapper must both MANUFACTURE a visit the inner never emits and SUPPRESS one it
  /// did) and the deferred consumer strategy.</para>
  /// </summary>
  internal class WhereBreadthFirstTreenumerator<TNode>
    : TreenumeratorWrapper<TNode>
  {
    public WhereBreadthFirstTreenumerator(
      Func<ITreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate,
      NodeTraversalStrategies nodeTraversalStrategy)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
      _NodeTraversalStrategy = nodeTraversalStrategy;

      // Seed the path with a sentinel root taken from the inner treenumerator's initial position.
      _Path = new WhereBreadthFirstPath<TNode>(InnerTreenumerator.Node, InnerTreenumerator.Position);
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;
    private readonly NodeTraversalStrategies _NodeTraversalStrategy;

    // Non-readonly so calls bind `ref this` and the struct's state mutations persist (a readonly field
    // would force a defensive copy and silently lose them -- see DepthFirstTreenumerator.cs:37-39).
    private WhereBreadthFirstPath<TNode> _Path;

    // The front (active effective parent)'s return-visit cadence against the inner stream.
    // The wrapper must do TWO things the base engine never does: MANUFACTURE a visit the inner
    // never emits (Owed), and SUPPRESS a visit the inner DID emit (SuppressNextInner). Distinct,
    // not one bit.
    private enum FrontReturnVisit
    {
      None,               // Front owes nothing; pass inner parent visits through untouched.
      Owed,               // A promoted child was scheduled; manufacture the front's return visit next.
      SuppressNextInner,  // An Owed return visit was CANCELLED by a consumer SkipNode (earlier promoted
                          // siblings already advanced the front past the inner's between-children visit);
                          // swallow that now-redundant inner parent visit ONCE. Armed ONLY by the
                          // consumer-skip-cancel gate -- the normal emit path does NOT arm it (it relies
                          // on the Mode==Visiting / VisitCount comparison instead).
    }
    private FrontReturnVisit _FrontReturnVisit = FrontReturnVisit.None;

    // The consumer strategy held over a manufactured (Owed) parent visit, to be applied to the
    // child's own visiting turn that follows it.
    private NodeTraversalStrategies? _DeferredStrategy = null;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Output the deferred schedule from a consumer-SkipNode'd parent transition.
      if (_Path.ConsumerSkipDeferredSchedulePending)
      {
        _Path.ClearConsumerSkippedSubtree();

        Publish(ref _Path.Back, TreenumeratorMode.SchedulingNode);

        return true;
      }

      if (Mode == TreenumeratorMode.VisitingNode)
      {
        if (_DeferredStrategy.HasValue)
        {
          nodeTraversalStrategies = _DeferredStrategy.Value;
          _DeferredStrategy = null;
        }
        else
        {
          nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;
        }
      }

      var previouslySeenNodeWasScheduledAndSkipped =
        !Position.IsForestRoot
        && InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode);

      if (previouslySeenNodeWasScheduledAndSkipped)
        _Path.Back.TraversalStrategy = nodeTraversalStrategies;

      // SkipSiblings now passes straight through to the base engine, which handles it
      // correctly under promotion (including effective-root SkipSiblings ending the root
      // stream). The wrapper previously stripped and re-emulated it -- that was
      // over-compensation for the then-broken base engine and produced wrong results.

      // Manufacture the front's owed return visit (unless this turn consumer-SkipNode's the child,
      // which cancels the owing, or the front is the sentinel).
      if (_FrontReturnVisit == FrontReturnVisit.Owed && !previouslySeenNodeWasScheduledAndSkipped)
      {
        // Inlines _Path.Front (no ref local) so this stays identical to the async source of truth,
        // where by-reference locals are illegal (CS8177). Front ref-returns the same slot each
        // call, so this is semantically identical to holding a ref local.
        if (_Path.Front.Position.Depth >= 0)
        {
          // The manufactured visit is emitted here. The inner's redundant between-children parent
          // visit is suppressed downstream by the Mode==Visiting / VisitCount comparison, not by
          // SuppressNextInner (that token is reserved for the consumer-skip-cancel 32b1f8f gate).
          _FrontReturnVisit = FrontReturnVisit.None;
          _DeferredStrategy = nodeTraversalStrategies;
          _Path.Front.VisitCount++;

          Publish(ref _Path.Front, TreenumeratorMode.VisitingNode);
          return true;
        }
      }

      if (previouslySeenNodeWasScheduledAndSkipped)
      {
        // THE 32b1f8f GATE. A consumer-skip that cancels an Owed return visit means we already
        // manufactured the front's between-children visit for this slot's earlier promoted siblings,
        // so the inner's matching parent visit is now redundant: arm its one-shot suppression. The
        // transition is Owed -> SuppressNextInner ONLY -- a not-Owed front leaves the inner's owed
        // visit to pass through, so a None-origin consumer-skip can never arm suppression and the
        // 32b1f8f swallowed-visit bug is structurally impossible.
        if (_FrontReturnVisit == FrontReturnVisit.Owed)
          _FrontReturnVisit = FrontReturnVisit.SuppressNextInner;

        if (_Path.ConsumerSkippedSubtreeDepth >= 0)
          _Path.MarkConsumerSkippedChildAfterLastAccepted();
      }

      // Owed but never emitted (the emit block fell through on the sentinel front): drop it.
      if (_FrontReturnVisit == FrontReturnVisit.Owed)
        _FrontReturnVisit = FrontReturnVisit.None;

      while (InnerTreenumerator.MoveNext(nodeTraversalStrategies))
      {
        if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        {
          var innerDepth = InnerTreenumerator.Position.Depth;

          var skipped = !_Predicate(InnerTreenumerator.ToNodeContext());

          _Path.PrefixWriteForScheduledNode(innerDepth, skipped);

          if (skipped)
          {
            nodeTraversalStrategies = _NodeTraversalStrategy;

            continue;
          }

          // --- Accepted node processing ---

          // Remove consumer-SkipNode'd nodes before calculating effective position.
          var lastScheduleNodeVisitWasSkipped = _Path.Back.Skipped;
          var removedSkippedDepth = lastScheduleNodeVisitWasSkipped
            ? _Path.Back.Position.Depth
            : -1;

          if (lastScheduleNodeVisitWasSkipped)
          {
            // Record the removed parent at its effective depth so its children and
            // subsequent siblings at the same depth get correct sibling indices.
            _Path.BeginRemovedSkippedParent(removedSkippedDepth);
          }

          var effectivePosition = _Path.GetEffectivePosition(innerDepth, out var immediateParentIsSkipped);

          _Path.RecordAcceptedChild(effectivePosition);

          // Reset the front's cadence for this freshly accepted child. A PROMOTED child (its
          // immediate inner parent was predicate-filtered, and it is not itself an effective root)
          // owes the front a manufactured return visit; anything else clears the token, dropping any
          // prior suppression.
          _FrontReturnVisit = immediateParentIsSkipped && effectivePosition.Depth > 0
            ? FrontReturnVisit.Owed
            : FrontReturnVisit.None;

          // An accepted child within the SkipNode'd subtree clears the flag.
          if (_Path.ConsumerSkippedSubtreeDepth >= 0
            && effectivePosition.Depth > _Path.ConsumerSkippedSubtreeDepth)
            _Path.ClearConsumerSkippedChildAfterLastAccepted();

          // Track entry into a consumer-SkipNode'd subtree.
          if (lastScheduleNodeVisitWasSkipped && removedSkippedDepth >= 0
            && effectivePosition.Depth > removedSkippedDepth)
          {
            _Path.EnterConsumerSkippedSubtree(removedSkippedDepth);
          }
          else if (_Path.ConsumerSkippedSubtreeDepth >= 0
            && effectivePosition.Depth <= _Path.ConsumerSkippedSubtreeDepth
            && effectivePosition.Depth > 0
            && Mode != TreenumeratorMode.VisitingNode
            && _Path.Front.Position.Depth >= 0)
          {
            if (!_Path.ConsumerSkippedChildAfterLastAccepted)
            {
              _Path.ClearConsumerSkippedChildAfterLastAccepted();

              _Path.EnqueueAccepted(InnerTreenumerator.Node, effectivePosition, innerDepth);
              _Path.MarkDeferredSchedulePending();

              _Path.Front.VisitCount++;

              Publish(ref _Path.Front, TreenumeratorMode.VisitingNode);

              return true;
            }

            // The last child was consumer-SkipNode'd, so no deferred V parent needed.
            _Path.ClearConsumerSkippedSubtree();
            _Path.ClearConsumerSkippedChildAfterLastAccepted();
          }

          _Path.EnqueueAccepted(InnerTreenumerator.Node, effectivePosition, innerDepth);
        }
        else // VisitingNode
        {
          // Swallow the inner's now-redundant between-children parent visit ONCE.
          if (_FrontReturnVisit == FrontReturnVisit.SuppressNextInner
            && InnerTreenumerator.VisitCount > 1)
          {
            _FrontReturnVisit = FrontReturnVisit.None;

            if (_Path.Front.VisitCount >= InnerTreenumerator.VisitCount)
              continue;
          }

          if (InnerTreenumerator.VisitCount == 1)
          {
            _Path.RetireFrontAndReanchor();
          }
          else if (Mode == TreenumeratorMode.VisitingNode)
            continue;

          _Path.Front.VisitCount++;
        }

        Publish(ref _Path.SelectPublishFrame(InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode), InnerTreenumerator.Mode);

        return true;
      }

      Publish(ref _Path.SelectPublishFrame(InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode), InnerTreenumerator.Mode);

      return false;
    }

    // The single publish funnel. Takes an EXPLICIT mode: the BFT wrapper publishes manufactured and
    // deferred-schedule visits whose VisitCount does not match their mode (a deferred SCHEDULE carries a
    // nonzero VisitCount), so the mode cannot be derived from VisitCount the way the DFT wrapper does.
    private void Publish(ref WhereBreadthFirstPath<TNode>.AcceptedFrame frame, TreenumeratorMode mode)
    {
      Mode = mode;
      Node = frame.Node;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }
  }
}
