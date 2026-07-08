using Copse;
using Copse.Core;
using System;

namespace Copse.Linq.Treenumerators
{
  // Breadth-first structural merge (Union). Zips the two operands' BFT visit streams into the BFT
  // traversal of their sibling-index OVERLAY tree.
  //
  // The two inners are themselves base BFT engines, so each produces -- for its own tree, with the
  // consumer's per-(merged-node) strategy forwarded -- the correct BFT choreography: a node's children
  // are SCHEDULED while that node is the inner's front, interleaved only with that node's own
  // between-children revisits, then the node retires. The merge follows both streams in lockstep and
  // emits the OVERLAY tree's choreography, OWNING the revisit cadence: it cannot forward an inner's
  // revisits, because a merged node's child slots are the UNION (by sibling index) of its two inner
  // nodes' slots, and a merged slot earns a revisit iff EITHER side's slot enqueued an accepted node
  // (e.g. a consumer-SkipNode'd internal node that promotes a descendant).
  //
  // MATCHING BY CONSUMED VISIT COUNT, NOT RAW POSITION. Raw positions COLLIDE across subtrees under
  // SkipNode promotion: a promoted node and a real node can share the same (sibling, depth), and two
  // CONSECUTIVE inner visits can share a position. So the merge does NOT match an inner visit to a frame
  // by position. Instead it consumes both inner streams in strict merged order, tracking per frame how
  // many of each inner node's visits it has folded in (Left/RightConsumedVC). A present side's CURRENT
  // visit belongs to the front iff it is a Visiting node at the front's depth whose VisitCount is exactly
  // ConsumedVC + 1 (the node's next visit). Any other state -- a deeper descendant (the front retired in
  // the inner and a child is now its front), a same-depth sibling (VisitCount resets to 1), a scheduling
  // node -- means the front is done on that side. Order guarantees the inner is never advanced past a
  // visit the front still needs, so the position collisions are benign.
  //
  // SKIPSIBLINGS CROSS-PROPAGATION. A consumer SkipSiblings on a merged node N means "N's effective
  // parent has no more children after N", so EVERY later merged sibling of N is dropped regardless of
  // which operand it comes from. The strategy is forwarded to the side(s) N HAS (their inner drops its
  // own remaining siblings), but a single-sided N's later siblings can live in the OTHER operand. So the
  // merge CAPS N's effective parent (the front when N was scheduled, or the forest for an effective root)
  // and, while that parent is still active, PRUNES (SkipNodeAndDescendants) every further node it
  // schedules at N's raw depth or SHALLOWER -- those are N's later effective siblings on either operand.
  // Nodes scheduled DEEPER than N are N's own promoted descendants (SkipNodeAndSiblings keeps them). A
  // capped front owes no further child slots, so it retires once both sides leave its sibling levels.
  internal class StructuralMergeBreadthFirstTreenumerator<TLeft, TRight>
    : TreenumeratorBase<MergeNode<TLeft, TRight>>
  {
    public StructuralMergeBreadthFirstTreenumerator(
      Func<ITreenumerator<TLeft>> leftTreenumeratorFactory,
      Func<ITreenumerator<TRight>> rightTreenumeratorFactory)
    {
      _LeftTreenumerator = leftTreenumeratorFactory();
      _RightTreenumerator = rightTreenumeratorFactory();
    }

    private readonly ITreenumerator<TLeft> _LeftTreenumerator;
    private readonly ITreenumerator<TRight> _RightTreenumerator;

    private bool _LeftTreenumeratorFinished;
    private bool _RightTreenumeratorFinished;

    private bool _BothTreenumeratorsFinished => _LeftTreenumeratorFinished && _RightTreenumeratorFinished;

    // The merged visit queue. The front is the active merged parent (the node currently being revisited
    // between its child slots). Newly scheduled merged children are appended to the back.
    private readonly RefSemiDeque<MergedFrame> _Queue = new RefSemiDeque<MergedFrame>();

    // The side(s) of the most recently emitted merged scheduling visit, whose inner(s) still sit at the
    // just-scheduled node and must be advanced (with the consumer's strategy) at the start of the next
    // step. Scheduling does not advance the inner -- it is read in place -- so the consumer's strategy,
    // which arrives on the NEXT MoveNext, is forwarded to exactly the right inner.
    private bool _PendingAdvanceLeft;
    private bool _PendingAdvanceRight;

    // The forest (root) level has no parent frame to batch its siblings, so -- exactly like the base
    // engine's _RootsScheduled phase -- the merge must schedule the WHOLE root frontier (every root, plus
    // any children promoted from a consumer-SkipNode'd root) before VISITING any of them. An inner has
    // finished scheduling its root frontier once it produces its first VISITING visit (BFT schedules the
    // entire frontier before the first visit). Until BOTH inners have (or finished), the merge only
    // schedules -- it does not visit or retire.
    private bool _RootsScheduled;

    // A consumer SkipSiblings on an effective-ROOT node caps the forest frontier (roots have no parent
    // frame): every further root / promoted-from-skipped-root child at the capped node's raw depth or
    // shallower is dropped.
    private bool _RootSiblingsCapped;
    private int _RootCapNodeRawDepth;

    // The raw position of the most recently emitted merged scheduling node -- the node a consumer
    // SkipSiblings (arriving on the NEXT MoveNext) would cap. Captured so the cap records its raw depth.
    private int _LastScheduledRawDepth;

    // Whether the most recently emitted merged scheduling node is an effective ROOT (scheduled before the
    // root frontier finished). Routes a consumer SkipSiblings to the forest cap vs the front frame's cap.
    private bool _LastScheduledWasRoot;

    // A merged frame on the visit queue: the published visit state plus the bookkeeping needed to own the
    // overlay revisit cadence.
    private struct MergedFrame
    {
      public MergeNode<TLeft, TRight> Node;
      public NodePosition Position;
      public int VisitCount;

      // How many of this node's left/right inner visits the merge has folded into the merged frame so far
      // (its inner VisitCount at the last consumption). The next genuine inner visit of this node is
      // ConsumedVC + 1. -1 when the side is absent.
      public int LeftConsumedVC;
      public int RightConsumedVC;

      // Sibling index of the most recent DIRECT child (at Position.Depth + 1) scheduled on each side. A
      // side's between-children revisit completes the slot named by this index. Promoted descendants
      // (deeper than the direct-child level) do not change it -- they belong to the current direct child's
      // slot. -1 = no direct child scheduled yet on that side.
      public int LeftLastDirectChildSibling;
      public int RightLastDirectChildSibling;

      // Highest merged slot index for which a between-children revisit has already been emitted. The
      // initial visit is slot -1 (VisitCount 0 -> 1). Coalesces the two sides' completions of the same
      // slot into one merged revisit.
      public int RevisitedSlot;

      // A consumer SkipSiblings dropped this node's remaining children. While this front is active every
      // further node scheduled is one of its effective children (a later sibling of the SkipSiblings'd
      // node), at the SkipSiblings'd node's raw depth or SHALLOWER (a less-promoted sibling). Those are
      // pruned on BOTH operands -- reaching the missing-side operand's siblings the forwarded strategy
      // never touched. Nodes scheduled DEEPER than the SkipSiblings'd node are that node's own promoted
      // descendants (SkipNodeAndSiblings) and are KEPT. CapNodeRawDepth is that node's raw depth.
      public bool SiblingsCapped;
      public int CapNodeRawDepth;

      public bool HasLeft => Node.HasLeft;
      public bool HasRight => Node.HasRight;
    }

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // The strategy applies to the node JUST scheduled (the last published S visit). If the consumer
      // SkipNode'd it, the merged frame we enqueued for it must be removed -- the inners (which receive
      // the same strategy below) promote its children into its slot with raw positions, or prune them
      // under SkipNodeAndDescendants. A single SkipNode test (a superset bit of the compound) covers both.
      if (Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Queue.RemoveLast();
      }

      // A consumer SkipSiblings on the just-scheduled node caps its EFFECTIVE PARENT: every later merged
      // sibling (a further child of that parent, on EITHER operand) is dropped. The strategy is forwarded
      // only to the side(s) the node HAS (so their own remaining siblings drop on their inner); the cap
      // additionally reaches the missing-side operand's co-positioned later siblings, which forwarding
      // never touches. The cap is set AFTER the just-scheduled node, so its slot is still emitted; only
      // later sibling slots are dropped.
      if (Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
      {
        if (_LastScheduledWasRoot)
        {
          _RootSiblingsCapped = true;
          _RootCapNodeRawDepth = _LastScheduledRawDepth;
        }
        else if (_Queue.Count > 0)
        {
          ref var front = ref _Queue.GetFirst();
          front.SiblingsCapped = true;
          front.CapNodeRawDepth = _LastScheduledRawDepth;
        }
      }

      // Advance the just-scheduled inner side(s) off the scheduled node, forwarding the consumer's
      // strategy to them (SkipNode/SkipDescendants/SkipSiblings/... all flow through unchanged to the
      // side(s) the node HAS).
      if (_PendingAdvanceLeft && !_LeftTreenumeratorFinished && !_LeftTreenumerator.MoveNext(nodeTraversalStrategies))
        _LeftTreenumeratorFinished = true;
      if (_PendingAdvanceRight && !_RightTreenumeratorFinished && !_RightTreenumerator.MoveNext(nodeTraversalStrategies))
        _RightTreenumeratorFinished = true;
      _PendingAdvanceLeft = false;
      _PendingAdvanceRight = false;

      while (true)
      {
        UpdateRootsScheduled();

        // 0) Prune a later sibling dropped by a SkipSiblings cap (a direct child of a capped effective
        //    parent on either operand). This silently advances the contributing inner(s); the loop
        //    re-evaluates without emitting.
        if (TryPruneCappedChild())
          continue;

        // 1) Retire any fully-visited fronts (a retire is a pure queue op -- no inner advance needed).
        //    Deferred until the whole root frontier is scheduled (root-level nodes have no parent batch).
        if (_RootsScheduled)
          while (_Queue.Count > 0 && FrontIsFullyVisited())
            _Queue.RemoveFirst();

        if (_BothTreenumeratorsFinished && _Queue.Count == 0)
          return false;

        // 2) Emit an owed merged revisit (initial or between-children) for the current front. Deferred
        //    during the root frontier scheduling phase.
        if (_RootsScheduled && _Queue.Count > 0 && TryEmitOwedRevisit())
          return true;

        // 3) Emit a merged scheduling visit by zipping the inner scheduling side(s). The scheduled
        //    inner(s) are advanced (with the consumer's strategy) at the start of the next step.
        if (TryEmitScheduledChild())
          return true;

        // 4) Nothing emittable from the current inner state: advance the inners and retry.
        AdvanceInners();
      }
    }

    // If a scheduling side is at a DIRECT child of a capped effective parent (the capped front, or a root
    // under a capped forest), it is a later sibling dropped by SkipSiblings: prune it (and its subtree)
    // off that inner via SkipNodeAndDescendants, on whichever side(s) are at it. Returns true iff it
    // pruned (the driver loops).
    private bool TryPruneCappedChild()
    {
      var leftScheduling = SideScheduling(_LeftTreenumerator, _LeftTreenumeratorFinished);
      var rightScheduling = SideScheduling(_RightTreenumerator, _RightTreenumeratorFinished);

      if (!leftScheduling && !rightScheduling)
        return false;

      // Determine which side(s) would be zipped next (the join), matching TryEmitScheduledChild.
      var atLeft = false;
      var atRight = false;
      if (leftScheduling && rightScheduling)
      {
        if (_LeftTreenumerator.Position >= _RightTreenumerator.Position) atLeft = true;
        if (_RightTreenumerator.Position >= _LeftTreenumerator.Position) atRight = true;
      }
      else if (leftScheduling) atLeft = true;
      else atRight = true;

      var depth = atLeft ? _LeftTreenumerator.Position.Depth : _RightTreenumerator.Position.Depth;

      // A later sibling of the capped node is an effective child of the capped (effective) parent
      // scheduled WHILE that parent is active, at the capped node's raw depth or SHALLOWER (a less- or
      // equally-promoted sibling). Nodes scheduled DEEPER than the capped node are that node's own
      // promoted descendants (SkipNodeAndSiblings) and are KEPT.
      //
      // The ROOT cap applies ONLY during the root-frontier phase: every root (and root-promotion) is
      // scheduled before _RootsScheduled, and an EARLIER root's children (also at depth >= 1) are not
      // scheduled until that root is the front -- AFTER the frontier -- so the root cap must not reach
      // them.
      bool capped;
      if (!_RootsScheduled && _RootSiblingsCapped && depth >= 0 && depth <= _RootCapNodeRawDepth)
        capped = true;
      else if (_Queue.Count > 0 && _Queue.GetFirst().SiblingsCapped
        && depth > _Queue.GetFirst().Position.Depth && depth <= _Queue.GetFirst().CapNodeRawDepth)
        capped = true;
      else
        capped = false;

      if (!capped)
        return false;

      if (atLeft && !_LeftTreenumerator.MoveNext(NodeTraversalStrategies.SkipNodeAndDescendants))
        _LeftTreenumeratorFinished = true;
      if (atRight && !_RightTreenumerator.MoveNext(NodeTraversalStrategies.SkipNodeAndDescendants))
        _RightTreenumeratorFinished = true;

      return true;
    }

    // Mark the root frontier as fully scheduled once both inners have left their root-scheduling phase --
    // each signalled by producing its first VISITING visit (or finishing). Latches true.
    private void UpdateRootsScheduled()
    {
      if (_RootsScheduled)
        return;

      var leftDone = _LeftTreenumeratorFinished || _LeftTreenumerator.Mode == TreenumeratorMode.VisitingNode;
      var rightDone = _RightTreenumeratorFinished || _RightTreenumerator.Mode == TreenumeratorMode.VisitingNode;

      if (leftDone && rightDone)
        _RootsScheduled = true;
    }

    // Advance the inner side(s) that sit at the join. When both are scheduling, advance the one(s) at the
    // max position (BFT order); when one is scheduling, advance it; when both are visiting, advance only
    // the present side(s) whose CURRENT visit is the front's next visit (so the merge never skips a frame
    // it has not finished).
    private void AdvanceInners()
    {
      // Raw scheduling mode here (NOT the depth>=0 "real node" guard) so the pre-root sentinel (depth -1)
      // is advanced to the first root.
      var leftScheduling = !_LeftTreenumeratorFinished && _LeftTreenumerator.Mode == TreenumeratorMode.SchedulingNode;
      var rightScheduling = !_RightTreenumeratorFinished && _RightTreenumerator.Mode == TreenumeratorMode.SchedulingNode;

      bool advanceLeft = false, advanceRight = false;

      if (leftScheduling && rightScheduling)
      {
        if (_LeftTreenumerator.Position >= _RightTreenumerator.Position)
          advanceLeft = true;
        if (_RightTreenumerator.Position >= _LeftTreenumerator.Position)
          advanceRight = true;
      }
      else if (leftScheduling)
      {
        advanceLeft = true;
      }
      else if (rightScheduling)
      {
        advanceRight = true;
      }
      else if (_Queue.Count > 0)
      {
        // Both visiting (or finished). Advance only the present side(s) whose current visit is the front's
        // next owed visit -- pushing it toward the next revisit / its retirement signal. Never advance a
        // side that has already moved off the front (a deeper descendant or a sibling): that belongs to a
        // LATER frame and must not be consumed now.
        ref var front = ref _Queue.GetFirst();
        if (front.HasLeft && SideHasNextFrontVisit(_LeftTreenumerator, _LeftTreenumeratorFinished, front.Position.Depth, front.LeftConsumedVC))
          advanceLeft = true;
        if (front.HasRight && SideHasNextFrontVisit(_RightTreenumerator, _RightTreenumeratorFinished, front.Position.Depth, front.RightConsumedVC))
          advanceRight = true;
      }

      if (advanceLeft && !_LeftTreenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        _LeftTreenumeratorFinished = true;
      if (advanceRight && !_RightTreenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        _RightTreenumeratorFinished = true;
    }

    // A side is scheduling a real node (past the pre-root sentinel at depth -1).
    private static bool SideScheduling<T>(ITreenumerator<T> t, bool finished)
      => !finished && t.Mode == TreenumeratorMode.SchedulingNode && t.Position.Depth >= 0;

    // The side's CURRENT visit is the front's next owed visit: a Visiting node at the front's depth whose
    // VisitCount is exactly ConsumedVC + 1 (the node's next visit). This order+VC match replaces raw
    // position matching (raw positions collide across subtrees under promotion).
    private static bool SideHasNextFrontVisit<T>(ITreenumerator<T> t, bool finished, int frontDepth, int consumedVC)
      => !finished
        && t.Mode == TreenumeratorMode.VisitingNode
        && t.Position.Depth == frontDepth
        && t.VisitCount == consumedVC + 1;

    // A side is still inside the front's CURRENT child slot -- promoting a descendant of a
    // consumer-SkipNode'd child -- when it is SCHEDULING a node strictly deeper than the front's
    // direct-child level (front.depth + 1). The merged between-children revisit is held while either
    // present side is still promoting into the slot, so both sides finish the slot before the revisit
    // fires (equal-slot siblings re-pair). A side VISITING a deeper node has DESCENDED past the front.
    private static bool SideStillInCurrentSlot<T>(ITreenumerator<T> t, bool finished, int frontDepth, bool present)
      => present
        && !finished
        && t.Mode == TreenumeratorMode.SchedulingNode
        && t.Position.Depth > frontDepth + 1;

    // Emit the front's next owed visit (initial vc1, or a between-children revisit) if a present side now
    // signals it. Consuming a side's visit == advancing the inner past the folded visit, so it moves on
    // to scheduling the node's children / its next revisit / its retirement.
    private bool TryEmitOwedRevisit()
    {
      // Mutations (ConsumedVC, VisitCount, RevisitedSlot) land in place via the ref-returning
      // _Queue.GetFirst(), inlined at every use rather than held in a `ref var front` local -- the
      // async source of truth cannot hold a by-reference local across its awaited AdvancePastConsumed
      // calls (CS8177), so the generated twin inlines it and this stays identical.
      var frontDepth = _Queue.GetFirst().Position.Depth;

      if (_Queue.GetFirst().VisitCount == 0)
      {
        // Initial visit: fire once each present side has reached its node's initial visit (its next visit,
        // ConsumedVC 0 -> 1).
        var leftReady = !_Queue.GetFirst().HasLeft || SideHasNextFrontVisit(_LeftTreenumerator, _LeftTreenumeratorFinished, frontDepth, _Queue.GetFirst().LeftConsumedVC);
        var rightReady = !_Queue.GetFirst().HasRight || SideHasNextFrontVisit(_RightTreenumerator, _RightTreenumeratorFinished, frontDepth, _Queue.GetFirst().RightConsumedVC);

        if (!leftReady || !rightReady)
          return false;

        if (_Queue.GetFirst().HasLeft) { _Queue.GetFirst().LeftConsumedVC++; AdvanceLeftPastConsumed(); }
        if (_Queue.GetFirst().HasRight) { _Queue.GetFirst().RightConsumedVC++; AdvanceRightPastConsumed(); }
        _Queue.GetFirst().VisitCount = 1;
        PublishFromFrame(_Queue.GetFirst(), TreenumeratorMode.VisitingNode);
        return true;
      }

      // Between-children revisit. Hold while either present side is still promoting strictly deeper than
      // the front's direct-child level (still inside the current slot's subtree) so both sides finish the
      // slot first.
      if (SideStillInCurrentSlot(_LeftTreenumerator, _LeftTreenumeratorFinished, frontDepth, _Queue.GetFirst().HasLeft)
        || SideStillInCurrentSlot(_RightTreenumerator, _RightTreenumeratorFinished, frontDepth, _Queue.GetFirst().HasRight))
        return false;

      // A present side at its next between-children revisit completes the slot named by that side's last
      // direct child. Coalesce the two sides by sibling index: emit ONE merged revisit for the slot, and
      // consume the revisit on every present side that is at a revisit for that slot.
      var leftAtRevisit = _Queue.GetFirst().HasLeft
        && SideHasNextFrontVisit(_LeftTreenumerator, _LeftTreenumeratorFinished, frontDepth, _Queue.GetFirst().LeftConsumedVC);
      var rightAtRevisit = _Queue.GetFirst().HasRight
        && SideHasNextFrontVisit(_RightTreenumerator, _RightTreenumeratorFinished, frontDepth, _Queue.GetFirst().RightConsumedVC);

      var candidate = -1;
      if (leftAtRevisit && _Queue.GetFirst().LeftLastDirectChildSibling > _Queue.GetFirst().RevisitedSlot)
        candidate = Math.Max(candidate, _Queue.GetFirst().LeftLastDirectChildSibling);
      if (rightAtRevisit && _Queue.GetFirst().RightLastDirectChildSibling > _Queue.GetFirst().RevisitedSlot)
        candidate = Math.Max(candidate, _Queue.GetFirst().RightLastDirectChildSibling);

      if (candidate < 0)
        return false;

      if (leftAtRevisit && _Queue.GetFirst().LeftLastDirectChildSibling == candidate)
      {
        _Queue.GetFirst().LeftConsumedVC++;
        AdvanceLeftPastConsumed();
      }
      if (rightAtRevisit && _Queue.GetFirst().RightLastDirectChildSibling == candidate)
      {
        _Queue.GetFirst().RightConsumedVC++;
        AdvanceRightPastConsumed();
      }

      _Queue.GetFirst().RevisitedSlot = candidate;
      _Queue.GetFirst().VisitCount++;
      PublishFromFrame(_Queue.GetFirst(), TreenumeratorMode.VisitingNode);
      return true;
    }

    // Advance an inner past a just-consumed (folded) visit. Revisits carry no consumer strategy.
    private void AdvanceLeftPastConsumed()
    {
      if (!_LeftTreenumeratorFinished && !_LeftTreenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        _LeftTreenumeratorFinished = true;
    }

    private void AdvanceRightPastConsumed()
    {
      if (!_RightTreenumeratorFinished && !_RightTreenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        _RightTreenumeratorFinished = true;
    }

    // Zip the inner scheduling side(s) at the join into one merged scheduling visit and enqueue it.
    private bool TryEmitScheduledChild()
    {
      var leftScheduling = SideScheduling(_LeftTreenumerator, _LeftTreenumeratorFinished);
      var rightScheduling = SideScheduling(_RightTreenumerator, _RightTreenumeratorFinished);

      if (!leftScheduling && !rightScheduling)
        return false;

      var includeLeft = false;
      var includeRight = false;

      if (leftScheduling && rightScheduling)
      {
        if (_LeftTreenumerator.Position >= _RightTreenumerator.Position)
          includeLeft = true;
        if (_RightTreenumerator.Position >= _LeftTreenumerator.Position)
          includeRight = true;
      }
      else if (leftScheduling)
      {
        includeLeft = true;
      }
      else
      {
        includeRight = true;
      }

      var node = new MergeNode<TLeft, TRight>(
        includeLeft ? _LeftTreenumerator.Node : default,
        includeRight ? _RightTreenumerator.Node : default,
        includeLeft,
        includeRight);

      var position = includeLeft ? _LeftTreenumerator.Position : _RightTreenumerator.Position;

      var frame = new MergedFrame
      {
        Node = node,
        Position = position,
        VisitCount = 0,
        LeftConsumedVC = includeLeft ? 0 : -1,
        RightConsumedVC = includeRight ? 0 : -1,
        LeftLastDirectChildSibling = -1,
        RightLastDirectChildSibling = -1,
        RevisitedSlot = -1,
      };

      // If this is a direct child of the current front, update the front's per-side slot index.
      if (_Queue.Count > 0)
      {
        ref var front = ref _Queue.GetFirst();
        if (position.Depth == front.Position.Depth + 1)
        {
          if (includeLeft)
            front.LeftLastDirectChildSibling = position.SiblingIndex;
          if (includeRight)
            front.RightLastDirectChildSibling = position.SiblingIndex;
        }
      }

      // An effective root is one scheduled before the root frontier is complete (it has no parent frame).
      _LastScheduledWasRoot = !_RootsScheduled;
      _LastScheduledRawDepth = position.Depth;

      _Queue.AddLast(frame);
      PublishFromFrame(frame, TreenumeratorMode.SchedulingNode);

      _PendingAdvanceLeft = includeLeft;
      _PendingAdvanceRight = includeRight;
      return true;
    }

    // The front is fully visited (ready to retire) when no present side still owes it a visit: each
    // present side has either finished, descended past the front (its next visit is no longer at the
    // front's depth / VC), or moved to a sibling -- and none is still scheduling a descendant.
    private bool FrontIsFullyVisited()
    {
      ref var front = ref _Queue.GetFirst();

      if (front.VisitCount == 0)
        return false;

      var frontDepth = front.Position.Depth;

      if (front.HasLeft && SideStillOwesFront(_LeftTreenumerator, _LeftTreenumeratorFinished, frontDepth, front.LeftConsumedVC, front.SiblingsCapped, front.CapNodeRawDepth))
        return false;

      if (front.HasRight && SideStillOwesFront(_RightTreenumerator, _RightTreenumeratorFinished, frontDepth, front.RightConsumedVC, front.SiblingsCapped, front.CapNodeRawDepth))
        return false;

      return true;
    }

    // A present side still owes the front more visits when its next visit is still the front's (a Visiting
    // node at the front's depth with VC == ConsumedVC + 1), or it is still scheduling a descendant of the
    // front (promoting into a slot). Otherwise the inner has moved off the front and owes it nothing.
    //
    // A SkipSiblings-CAPPED front owes nothing from scheduling a later SIBLING (a node at depth in
    // (frontDepth, capNodeRawDepth]): those are dropped, pruned by TryPruneCappedChild. But a side
    // scheduling DEEPER than the capped node (depth > capNodeRawDepth) is promoting a descendant of the
    // capped node into its OWN (kept) slot -- e.g. SkipNodeAndSiblings, where the node's promoted children
    // stay; the capped front still owes those. A still-owed Visiting revisit of a kept slot also owes.
    private static bool SideStillOwesFront<T>(ITreenumerator<T> t, bool finished, int frontDepth, int consumedVC, bool capped, int capNodeRawDepth)
    {
      if (finished)
        return false;

      if (SideHasNextFrontVisit(t, finished, frontDepth, consumedVC))
        return true;

      if (t.Mode == TreenumeratorMode.SchedulingNode && t.Position.Depth > frontDepth)
      {
        // For a capped front, a later-sibling level (depth <= capNodeRawDepth) does not owe; a deeper
        // promotion of the capped node's kept slot does.
        if (capped && t.Position.Depth <= capNodeRawDepth)
          return false;
        return true;
      }

      return false;
    }

    private void PublishFromFrame(MergedFrame frame, TreenumeratorMode mode)
    {
      Mode = mode;
      Node = frame.Node;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _LeftTreenumerator.Dispose();
      _RightTreenumerator.Dispose();
    }
  }
}
