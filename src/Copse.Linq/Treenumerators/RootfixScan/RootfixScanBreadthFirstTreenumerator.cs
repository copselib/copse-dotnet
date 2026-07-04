using Copse;
using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerators
{
  internal class RootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>
    : TreenumeratorWrapper<TNode, TAccumulate>
  {
    public RootfixScanBreadthFirstTreenumerator(
      Func<ITreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed) : base(innerTreenumeratorFactory)
    {
      _Accumulator = accumulator;

      var seedVisit =
        new NodeVisit<TAccumulate>(
          TreenumeratorMode.VisitingNode,
          seed,
          1,
          NodePosition.ForestRoot);

      _CurrentLevel.AddLast(seedVisit);
    }

    private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;

    private RefSemiDeque<NodeVisit<TAccumulate>> _CurrentLevel = new RefSemiDeque<NodeVisit<TAccumulate>>();
    private RefSemiDeque<NodeVisit<TAccumulate>> _NextLevel = new RefSemiDeque<NodeVisit<TAccumulate>>();

    private Stack<NodeVisit<TAccumulate>> _SkippedStack = new Stack<NodeVisit<TAccumulate>>();

    // Tracks whether we've scheduled any children since the last node was pushed to _SkippedStack.
    // This helps detect when we've moved to scheduling children of a different parent.
    private bool _ScheduledChildrenSinceSkip = false;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (Mode == TreenumeratorMode.SchedulingNode)
      {
        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
          _NextLevel.RemoveLast();
        else if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        {
          _SkippedStack.Push(_NextLevel.RemoveLast());
          _ScheduledChildrenSinceSkip = false;
        }
      }

      if (!InnerTreenumerator.MoveNext(nodeTraversalStrategies))
        return false;

      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        OnSchedulingNode();
      else
        OnVisitingNode();

      return true;
    }

    private void OnSchedulingNode()
    {
      var parentDepth = InnerTreenumerator.Position.Depth - 1;

      // Pop skipped items that are not the immediate parent
      while (_SkippedStack.Count > 0
        && _SkippedStack.Peek().Position.Depth != parentDepth)
      {
        _SkippedStack.Pop();
      }

      // When sibling index is 0 and we've already scheduled some children from the skipped node,
      // we've moved to a different parent's children. Pop from the skipped stack.
      if (InnerTreenumerator.Position.SiblingIndex == 0
        && _ScheduledChildrenSinceSkip
        && _SkippedStack.Count > 0)
      {
        _SkippedStack.Pop();
        _ScheduledChildrenSinceSkip = false;
      }

      // Find the parent node visit:
      // 1. If _CurrentLevel[0] has parent depth, use it (we're currently visiting the parent)
      // 2. Else if skipped stack has an item at parent depth, use it (parent was skipped)
      // 3. Else if _NextLevel has items at parent depth, use the first one
      //    (this happens when grandparent was skipped but parent wasn't yet visited)
      // 4. Else use _CurrentLevel[0] (for root nodes, parent is seed at depth -1)
      NodeVisit<TAccumulate> accumulateNodeVisit;
      if (_CurrentLevel.GetFirst().Position.Depth == parentDepth)
        accumulateNodeVisit = _CurrentLevel.GetFirst();
      else if (_SkippedStack.Count > 0)
        accumulateNodeVisit = _SkippedStack.Peek();
      else if (_NextLevel.Count > 0 && _NextLevel.GetFirst().Position.Depth == parentDepth)
        accumulateNodeVisit = _NextLevel.GetFirst();
      else
        accumulateNodeVisit = _CurrentLevel.GetFirst();

      var node = _Accumulator(accumulateNodeVisit.ToNodeContext(), InnerTreenumerator.ToNodeContext());

      var visit = InnerTreenumerator.ToNodeVisit().WithNode(node);

      _NextLevel.AddLast(visit);

      UpdateStateFromNodeVisit(visit);

      _ScheduledChildrenSinceSkip = true;
    }

    private void OnVisitingNode()
    {
      if (InnerTreenumerator.VisitCount == 1)
      {
        _SkippedStack.Clear();
        _CurrentLevel.RemoveFirst();

        if (_CurrentLevel.Count == 0)
          (_CurrentLevel, _NextLevel) = (_NextLevel, _CurrentLevel);

        // Remove items that were skipped by the inner treenumerator
        // (e.g., due to SkipSiblings affecting earlier siblings)
        while (_CurrentLevel.Count > 0
          && _CurrentLevel.GetFirst().Position != InnerTreenumerator.Position)
        {
          _CurrentLevel.RemoveFirst();
        }
      }

      ref var visit = ref _CurrentLevel.GetFirst();

      var newVisit =
        new NodeVisit<TAccumulate>(
          InnerTreenumerator.Mode,
          visit.Node,
          InnerTreenumerator.VisitCount,
          InnerTreenumerator.Position);

      visit = newVisit;

      UpdateStateFromNodeVisit(newVisit);
    }

    private void UpdateStateFromNodeVisit(NodeVisit<TAccumulate> nodeVisit)
    {
      Mode = nodeVisit.Mode;
      Node = nodeVisit.Node;
      VisitCount = nodeVisit.VisitCount;
      Position = nodeVisit.Position;
    }
  }
}
