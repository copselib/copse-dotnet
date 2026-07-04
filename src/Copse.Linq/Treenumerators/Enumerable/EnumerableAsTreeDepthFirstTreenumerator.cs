using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Treenumerators.Enumerator
{
  internal class EnumerableAsTreeDepthFirstTreenumerator<TNode> : ITreenumerator<TNode>
  {
    public EnumerableAsTreeDepthFirstTreenumerator(
      IEnumerable<TNode> enumerable)
    {
      _Enumerator = enumerable.GetEnumerator();
    }

    private IEnumerator<TNode> _Enumerator;
    private readonly RefSemiDeque<(TNode, int)> _Stack = new RefSemiDeque<(TNode, int)>();
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

      if (_Enumerator == null)
        return MoveUpStack();

      if (Mode == TreenumeratorMode.VisitingNode || _Stack.Count == 0)
        return OnVisiting();

      return OnScheduling(nodeTraversalStrategies);
    }

    private bool OnVisiting()
    {
      if (!EnumeratorMoveNext())
      {
        // This only happens for empty treenumerables
        if (_Stack.Count == 0)
        {
          _EnumerationFinished = true;
          return false;
        }

        _Stack.RemoveLast();

        return MoveUpStack();
      }

      AddScheduledNode();

      return true;
    }

    private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
      {
        DisposeEnumerator();

        if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        {
          _Stack.RemoveLast();
          return MoveUpStack();
        }
        else
        {
          VisitCount = 1;
          Mode = TreenumeratorMode.VisitingNode;
          _DepthOfLastVisitedNode = Position.Depth;
          _Stack.RemoveLast();
          return true;
        }
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Stack.RemoveLast();

        if (!EnumeratorMoveNext())
          return MoveUpStack();

        AddScheduledNode();

        return true;
      }

      VisitCount = 1;
      Mode = TreenumeratorMode.VisitingNode;
      _DepthOfLastVisitedNode = Position.Depth;
      return true;
    }

    private bool EnumeratorMoveNext()
    {
      if (_Enumerator?.MoveNext() == true)
        return true;

      DisposeEnumerator();

      return false;
    }

    private void AddScheduledNode()
    {
      Node = _Enumerator.Current;
      VisitCount = 0;
      Position = new NodePosition(0, Position.Depth + 1);
      Mode = TreenumeratorMode.SchedulingNode;

      _Stack.AddLast((Node, Position.Depth));
    }

    private bool MoveUpStack()
    {
      while (_Stack.Count > 0)
      {
        (var node, var depth) = _Stack.RemoveLast();

        // Only return V2 if we actually descended below this node.
        // If _DepthOfLastVisitedNode <= depth, all children were skipped
        // without being visited, so this node shouldn't get V2.
        if (_DepthOfLastVisitedNode > depth)
        {
          Node = node;
          VisitCount = 2;
          Position = new NodePosition(0, depth);
          Mode = TreenumeratorMode.VisitingNode;
          return true;
        }
      }

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
  }
}
