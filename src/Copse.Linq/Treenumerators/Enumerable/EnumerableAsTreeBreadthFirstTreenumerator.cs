using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerators.Enumerator
{
  internal class EnumerableAsTreeBreadthFirstTreenumerator<TNode> : ITreenumerator<TNode>
  {
    public EnumerableAsTreeBreadthFirstTreenumerator(
      IEnumerable<TNode> enumerable)
    {
      _Enumerator = enumerable.GetEnumerator();
    }

    private IEnumerator<TNode> _Enumerator;
    private readonly RefSemiDeque<NodeAndDepth> _Queue = new RefSemiDeque<NodeAndDepth>();
    private bool _EnumerationFinished = false;
    private int _DepthOfLastVisitedNode = -1;

    public TNode Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;
    public TreenumeratorMode Mode { get; private set; } = default;

    public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_EnumerationFinished)
        return false;

      if (_Queue.Count == 0)
        return OnEnumerationStarting();

      if (Mode == TreenumeratorMode.VisitingNode)
        return OnVisiting();

      return OnScheduling(nodeTraversalStrategies);
    }

    private bool OnEnumerationStarting()
    {
      if (EnumeratorMoveNext())
        return AddScheduledNode();
      else
        return FinishEnumeration();
    }

    private bool OnVisiting()
    {
      if (VisitCount == 1 && EnumeratorMoveNext())
        return AddScheduledNode();

      _Queue.RemoveFirst();

      if (_Queue.Count == 0)
        return FinishEnumeration();

      return VisitLeadNode();
    }

    private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
      {
        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        {
          _Queue.RemoveLast();

          if (_Queue.Count > 0)
            return TryVisitLeadNodeIfDescendantWasVisited();

          return FinishEnumeration();
        }

        VisitLeadNode();

        DisposeEnumerator();

        return true;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Queue.RemoveLast();

        if (EnumeratorMoveNext())
          return AddScheduledNode();

        if (_Queue.Count > 0)
          return TryVisitLeadNodeIfDescendantWasVisited();

        return FinishEnumeration();
      }

      return VisitLeadNode();
    }

    private bool EnumeratorMoveNext()
    {
      if (_Enumerator?.MoveNext() == true)
        return true;

      DisposeEnumerator();

      return false;
    }

    private bool VisitLeadNode()
    {
      if (_Queue.Count == 0)
        return FinishEnumeration();

      ref var nodeAndDepth = ref _Queue.GetFirst();

      nodeAndDepth.VisitCount++;

      Node = nodeAndDepth.Node;
      VisitCount = nodeAndDepth.VisitCount;
      Position = new NodePosition(0, nodeAndDepth.Depth);
      Mode = TreenumeratorMode.VisitingNode;

      // Track the depth of the last V1 visit
      if (VisitCount == 1)
        _DepthOfLastVisitedNode = nodeAndDepth.Depth;

      return true;
    }

    private bool TryVisitLeadNodeIfDescendantWasVisited()
    {
      // When a node is skipped, we need to check if the parent should get V2.
      // A parent only gets V2 if at least one descendant was visited (had V1).
      while (_Queue.Count > 0)
      {
        var nodeAndDepth = _Queue.GetFirst();

        // For V2+ (VisitCount > 0), only emit if a descendant at greater depth was visited
        if (nodeAndDepth.VisitCount > 0 && _DepthOfLastVisitedNode <= nodeAndDepth.Depth)
        {
          // No descendants were visited at a greater depth, skip this node's V2
          _Queue.RemoveFirst();
          continue;
        }

        // Otherwise, proceed with normal visit
        return VisitLeadNode();
      }

      return FinishEnumeration();
    }

    private bool AddScheduledNode()
    {
      Node = _Enumerator.Current;
      VisitCount = 0;
      Position = new NodePosition(0, Position.Depth + 1);
      Mode = TreenumeratorMode.SchedulingNode;

      _Queue.AddLast(new NodeAndDepth(Node, 0, Position.Depth));

      return true;
    }

    private bool FinishEnumeration()
    {
      _EnumerationFinished = true;
      return false;
    }

    public void Dispose()
    {
      DisposeEnumerator();
    }

    private void DisposeEnumerator()
    {
      _Enumerator?.Dispose();
      _Enumerator = null;
    }

    private struct NodeAndDepth
    {
      public NodeAndDepth(
        TNode node,
        int visitCount,
        int depth)
      {
        Node = node;
        VisitCount = visitCount;
        Depth = depth;
      }

      public TNode Node { get; }
      public int VisitCount { get; set; }
      public int Depth { get; }
    }
  }
}
