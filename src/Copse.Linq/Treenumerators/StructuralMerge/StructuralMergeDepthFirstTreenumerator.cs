using Copse.Core;
using System;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerators
{
  internal class StructuralMergeDepthFirstTreenumerator<TLeft, TRight>
    : TreenumeratorBase<MergeNode<TLeft, TRight>>
  {
    public StructuralMergeDepthFirstTreenumerator(
      Func<ITreenumerator<TLeft>> leftTreenumeratorFactory,
      Func<ITreenumerator<TRight>> rightTreenumeratorFactory)
    {
      _LeftTreenumerator = leftTreenumeratorFactory();
      _RightTreenumerator = rightTreenumeratorFactory();
    }

    private readonly ITreenumerator<TLeft> _LeftTreenumerator;
    private readonly ITreenumerator<TRight> _RightTreenumerator;

    private bool _LeftSiblingSkipPending = false;
    private bool _RightSiblingSkipPending = false;
    // Raw depth of the skipping node's effective parent. A stashed sibling-skip applies only to a missing-
    // side node that the operand schedules STRICTLY DEEPER than this (a genuine descendant of the shared
    // effective parent == a real effective sibling); if the operand instead schedules at or above this
    // depth it has left the parent's subtree (e.g. backtracked to a sibling root) and the pending skip is
    // discarded -- it does not silence nodes outside the parent.
    private int _SiblingSkipParentDepth = -1;

    private bool _LeftTreenumeratorFinished;
    private bool _RightTreenumeratorFinished;

    private bool _BothTreenumeratorsFinished => _LeftTreenumeratorFinished && _RightTreenumeratorFinished;

    private Stack<NodeVisit<MergeNode<TLeft, TRight>>> _NodeVisits = new Stack<NodeVisit<MergeNode<TLeft, TRight>>>();

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      HandleMoveNextForLeftAndRightTreenumerators(nodeTraversalStrategies);

      if (_BothTreenumeratorsFinished)
        return false;

      FixUpNodeVisitsStack(nodeTraversalStrategies);

      var nodeVisit = CreateNextNodeVisit();

      _NodeVisits.Push(nodeVisit);

      UpdateStateFromNodeVisit(nodeVisit);

      return true;
    }

    private void HandleMoveNextForLeftAndRightTreenumerators(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // SkipSiblings must silence the remaining EFFECTIVE siblings of the just-scheduled node N under
      // N's effective parent. The operand(s) N has are handled by the normal MoveNext below (the strategy
      // is forwarded to them). The operand N LACKS is NOT visited below (it has no node at N's Position),
      // so if it currently holds -- or is about to yield -- an effective sibling of N we must propagate
      // the skip to it explicitly. The effective parent is the stack entry directly below N (the clean
      // ancestor path keeps it accurate even across consumer SkipNode promotion, where N sits at a deeper
      // RAW depth than its effective parent); for an effective root it is the forest level (depth -1).
      //
      // The missing-side operand X holds/will-yield an effective sibling of N when either:
      //   * X is positioned strictly DEEPER than the effective parent (inside its subtree -- X is already
      //     at a sibling of N, possibly mid-promotion), OR
      //   * X is positioned exactly AT the effective parent (X backtracked to it and its NEXT child is a
      //     sibling of N -- this happens under asymmetric promotion when X promoted fewer children than
      //     the side N has, so X returned to the parent first).
      // We must NOT propagate when X is merely parked at an unrelated node at the effective parent's depth
      // but a different position (e.g. a sibling root of the effective parent) -- that node is not a
      // sibling of N. The stashed skip is applied below once X reaches sibling level.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
      {
        var effectiveParent = EffectiveParent();

        _SiblingSkipParentDepth = effectiveParent.Depth;

        if (!Node.HasLeft
          && !_LeftTreenumeratorFinished
          && HoldsEffectiveSibling(_LeftTreenumerator.Position, effectiveParent))
          _LeftSiblingSkipPending = true;

        if (!Node.HasRight
          && !_RightTreenumeratorFinished
          && HoldsEffectiveSibling(_RightTreenumerator.Position, effectiveParent))
          _RightSiblingSkipPending = true;
      }

      var callMoveNextOnLeftTreenumerator =
        !_LeftTreenumeratorFinished
        && (_NodeVisits.Count == 0
          || (Node.HasLeft && _LeftTreenumerator.Position == Position));

      var callMoveNextOnRightTreenumerator =
        !_RightTreenumeratorFinished
        && (_NodeVisits.Count == 0
          || (Node.HasRight && _RightTreenumerator.Position == Position));

      if (callMoveNextOnLeftTreenumerator
        && !_LeftTreenumerator.MoveNext(nodeTraversalStrategies))
      {
        _LeftTreenumeratorFinished = true;
      }

      if (callMoveNextOnRightTreenumerator
        && !_RightTreenumerator.MoveNext(nodeTraversalStrategies))
      {
        _RightTreenumeratorFinished = true;
      }

      // Resolve a stashed sibling-skip once the missing-side operand reaches a SCHEDULING node (the skip
      // strategy only takes effect on a node being SCHEDULED -- forwarding it while the operand is still
      // VISITING / backtracking to the shared parent would be consumed by the visit->schedule transition
      // and leave the sibling un-skipped, so we keep the flag pending until then). At the first scheduled
      // node we decide by depth:
      //   * deeper than the shared effective parent  -> a genuine effective sibling: apply the skip
      //     (SkipNodeAndDescendants|SkipSiblings drops it and its remaining siblings, stopping at the
      //     parent);
      //   * at or above the effective parent's depth -> the operand left the parent's subtree without
      //     yielding a sibling (it had no more children of the parent and backtracked to, e.g., a sibling
      //     root): DISCARD the pending -- it must not silence nodes outside the parent.
      if (_LeftSiblingSkipPending
        && !_LeftTreenumeratorFinished
        && _LeftTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
      {
        if (_LeftTreenumerator.Position.Depth > _SiblingSkipParentDepth)
        {
          if (!_LeftTreenumerator.MoveNext(NodeTraversalStrategies.SkipNodeAndDescendants | NodeTraversalStrategies.SkipSiblings))
            _LeftTreenumeratorFinished = true;
        }

        _LeftSiblingSkipPending = false;
      }

      if (_RightSiblingSkipPending
        && !_RightTreenumeratorFinished
        && _RightTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
      {
        if (_RightTreenumerator.Position.Depth > _SiblingSkipParentDepth)
        {
          if (!_RightTreenumerator.MoveNext(NodeTraversalStrategies.SkipNodeAndDescendants | NodeTraversalStrategies.SkipSiblings))
            _RightTreenumeratorFinished = true;
        }

        _RightSiblingSkipPending = false;
      }
    }

    private void FixUpNodeVisitsStack(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Pop the current node if it was skipped
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        _NodeVisits.Pop();

      // Keep _NodeVisits a clean root-to-current ANCESTOR path by popping any top that is no longer on
      // the current path. A top is off the path when neither treenumerator is at it or within its
      // subtree -- i.e. neither treenumerator is positioned at the top exactly (it is not owed a return
      // visit) and both are at or above its depth. Two cases collapse into this:
      //   * Backtracking past the top (both treenumerators strictly shallower) -- the original case.
      //   * Moving from a completed node to a SIBLING at the same depth -- the top is a finished sibling,
      //     not an ancestor, so it must be popped or it would masquerade as an ancestor (this is what
      //     broke consumer SkipNode child-promotion at the effective-root level: a promoted node's
      //     skipped parent was a root whose finished sibling root lingered and was mistaken for a real
      //     ancestor by EffectiveParent, suppressing the SkipSiblings propagation).
      // A top the treenumerators are still at (Position match) is owed its next return visit, so it must
      // NOT be popped.
      while (_NodeVisits.Count > 0)
      {
        var topPosition = _NodeVisits.Peek().Position;
        var leftDepth = _LeftTreenumeratorFinished ? -1 : _LeftTreenumerator.Position.Depth;
        var rightDepth = _RightTreenumeratorFinished ? -1 : _RightTreenumerator.Position.Depth;
        var maxTreenumeratorDepth = Math.Max(leftDepth, rightDepth);

        var treenumeratorAtTop =
          (!_LeftTreenumeratorFinished && _LeftTreenumerator.Position == topPosition)
          || (!_RightTreenumeratorFinished && _RightTreenumerator.Position == topPosition);

        if (maxTreenumeratorDepth <= topPosition.Depth && !treenumeratorAtTop)
          _NodeVisits.Pop();
        else
          break;
      }
    }

    private NodeVisit<MergeNode<TLeft, TRight>> CreateNextNodeVisit()
    {
      // DFS ordering invariant: a parent's between-children return visit must be emitted BEFORE the
      // parent's next child is scheduled. When the two operands desynchronize -- which happens when a
      // consumer SkipNode swallows an internal merged node whose left/right copies have different
      // shapes, so one operand promotes a child (descends) while the other moves to a sibling (stays
      // shallow) -- one operand can be sitting at the open parent's return visit while the other is
      // ready to schedule a deeper promoted child. The base engine would emit the parent return visit
      // first; without this guard the greedy "deepest scheduling node wins" logic below would drop it.
      if (HasPendingParentReturnVisit())
        return RevisitLeadNodeVisitInQueue();

      var nextNodeVisitHasLeft =
        !_LeftTreenumeratorFinished
          && (_RightTreenumeratorFinished
            || _LeftTreenumerator.Position.Depth > _RightTreenumerator.Position.Depth
            || (_LeftTreenumerator.Position.Depth == _RightTreenumerator.Position.Depth
              && _LeftTreenumerator.Position.SiblingIndex <= _RightTreenumerator.Position.SiblingIndex));

      var nextNodeVisitHasRight =
        !_RightTreenumeratorFinished
          && (_LeftTreenumeratorFinished
            || _RightTreenumerator.Position.Depth > _LeftTreenumerator.Position.Depth
            || (_RightTreenumerator.Position.Depth == _LeftTreenumerator.Position.Depth
              && _RightTreenumerator.Position.SiblingIndex <= _LeftTreenumerator.Position.SiblingIndex));

      var createSchedulingNodeVisit =
        (nextNodeVisitHasLeft && _LeftTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        || (nextNodeVisitHasRight && _RightTreenumerator.Mode == TreenumeratorMode.SchedulingNode);

      return
        createSchedulingNodeVisit
        ? CreateSchedulingNodeVisit(nextNodeVisitHasLeft, nextNodeVisitHasRight)
        : RevisitLeadNodeVisitInQueue();
    }

    // Whether the current open parent (the stack top) is owed -- and ready to receive -- its next
    // between-/after-children return visit. One operand owes it when it has backtracked to the parent.
    // The visit is READY only once the OTHER operand has also finished the parent's CURRENT child-slot:
    // under asymmetric / multi-level promotion (one or more consecutive consumer-SkipNode'd raw children,
    // whose left/right copies may differ in shape) one operand can still be promoting descendants out of
    // that slot while the other has already returned to the parent. We hold the return visit while the
    // non-owing operand is still inside the current slot, and release it once both operands have left it.
    private bool HasPendingParentReturnVisit()
    {
      if (_NodeVisits.Count == 0)
        return false;

      var parent = _NodeVisits.Peek();

      // Gate on the PARENT's sides, not the just-scheduled node's: only an operand whose side the parent
      // actually carries can have backtracked to the parent and owe its return visit. (Using the current
      // node's sides was wrong when that node is a single-sided promoted child being skipped -- e.g. the
      // last, childless promoted child of a multi-level-skipped slot -- which would mask the other
      // operand's genuinely-owed return visit and drop it.)
      var leftOwes = OwesReturnVisit(_LeftTreenumerator, _LeftTreenumeratorFinished, parent.Node.HasLeft, parent);
      var rightOwes = OwesReturnVisit(_RightTreenumerator, _RightTreenumeratorFinished, parent.Node.HasRight, parent);

      if (leftOwes
        && !StillInCurrentChildSlotOf(_RightTreenumerator, _RightTreenumeratorFinished, parent.Position.Depth))
        return true;

      if (rightOwes
        && !StillInCurrentChildSlotOf(_LeftTreenumerator, _LeftTreenumeratorFinished, parent.Position.Depth))
        return true;

      return false;
    }

    // Whether the operand is still inside the parent's CURRENT child-slot -- i.e. somewhere within the
    // subtree of the parent's current raw child (the one just being scheduled/promoted), at ANY promotion
    // depth.
    //
    // The slot boundary is depth-agnostic: a node's raw depth is always one greater than its raw parent's,
    // so the parent's own direct raw children sit at parentDepth + 1 REGARDLESS of how many of them are
    // SkipNode'd. The parent's next raw child therefore appears at exactly parentDepth + 1, while every
    // descendant promoted out of the current slot (a child/grandchild/... of an already-skipped raw child)
    // sits strictly deeper, at parentDepth + 2 or below. So "still in the current slot" is precisely
    // "raw depth strictly greater than the parent's direct-child level", which holds for promotion that
    // spans one level, two levels, or any number -- it never bakes in a fixed promotion span.
    private static bool StillInCurrentChildSlotOf<TInner>(
      ITreenumerator<TInner> treenumerator,
      bool finished,
      int parentDepth)
    {
      var parentDirectChildDepth = parentDepth + 1;
      return !finished && treenumerator.Position.Depth > parentDirectChildDepth;
    }

    private static bool OwesReturnVisit<TInner>(
      ITreenumerator<TInner> treenumerator,
      bool finished,
      bool parentHasThisSide,
      NodeVisit<MergeNode<TLeft, TRight>> parent)
      => !finished
        && parentHasThisSide
        && treenumerator.Mode == TreenumeratorMode.VisitingNode
        && treenumerator.Position == parent.Position
        && treenumerator.VisitCount == parent.VisitCount + 1;

    private NodeVisit<MergeNode<TLeft, TRight>> CreateSchedulingNodeVisit(
      bool includeLeft,
      bool includeRight)
    {
      var node =
        new MergeNode<TLeft, TRight>(
          includeLeft ? _LeftTreenumerator.Node : default,
          includeRight ? _RightTreenumerator.Node : default,
          includeLeft,
          includeRight);

      var nodeVisit =
        new NodeVisit<MergeNode<TLeft, TRight>>(
          TreenumeratorMode.SchedulingNode,
          node,
          includeLeft ? _LeftTreenumerator.VisitCount : _RightTreenumerator.VisitCount,
          includeLeft ? _LeftTreenumerator.Position : _RightTreenumerator.Position);

      return nodeVisit;
    }

    private NodeVisit<MergeNode<TLeft, TRight>> RevisitLeadNodeVisitInQueue()
    {
      var nodeVisit = _NodeVisits.Pop();

      nodeVisit =
        new NodeVisit<MergeNode<TLeft, TRight>>(
          TreenumeratorMode.VisitingNode,
          nodeVisit.Node,
          nodeVisit.VisitCount + 1,
          nodeVisit.Position);

      return nodeVisit;
    }

    private void UpdateStateFromNodeVisit(NodeVisit<MergeNode<TLeft, TRight>> nodeVisit)
    {
      Node = nodeVisit.Node;
      VisitCount = nodeVisit.VisitCount;
      Mode = nodeVisit.Mode;
      Position = nodeVisit.Position;
    }

    // The just-scheduled node's effective parent position: the nearest accepted ancestor on the clean
    // path, which is the stack entry directly below the top (the top being the node itself). Returns the
    // forest-root sentinel (depth -1) when the node has no accepted ancestor (an effective root). Because
    // the ancestor path is kept clean (finished siblings are popped in FixUpNodeVisitsStack), the entry
    // below the top is the genuine effective parent even when the node sits at a deeper RAW depth after
    // consumer SkipNode promotion.
    private NodePosition EffectiveParent()
    {
      if (_NodeVisits.Count < 2)
        return ForestRoot;

      var topVisit = _NodeVisits.Pop();
      var parent = _NodeVisits.Peek().Position;
      _NodeVisits.Push(topVisit);

      return parent.Depth < topVisit.Position.Depth ? parent : ForestRoot;
    }

    // Sentinel position for "above all roots" -- the effective parent of an effective-root node.
    private static readonly NodePosition ForestRoot = NodePosition.ForestRoot; // canonical name now lives in Core

    // Whether an operand parked at <paramref name="operandPosition"/> holds, or is about to yield, an
    // effective sibling of the just-scheduled node whose effective parent is <paramref name="parent"/>.
    // True when the operand is inside the effective parent's subtree (strictly deeper) or sitting exactly
    // at the effective parent (its next child will be a sibling). False when it is parked at an unrelated
    // node at the parent's own depth (e.g. a sibling root of the parent).
    private static bool HoldsEffectiveSibling(NodePosition operandPosition, NodePosition parent)
      => operandPosition.Depth > parent.Depth || operandPosition == parent;

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _LeftTreenumerator.Dispose();
      _RightTreenumerator.Dispose();
    }
  }
}
