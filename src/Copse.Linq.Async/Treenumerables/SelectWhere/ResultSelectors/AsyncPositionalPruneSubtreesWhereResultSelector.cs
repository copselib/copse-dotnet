using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // The positional PruneSubtreesWhere's result: the predicate sees (node, this layer's input labels).
  internal readonly struct AsyncPositionalPruneSubtreesWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPositionalPruneSubtreesWhereResultSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeAndPosition<TNode> nodeAndPosition)
      => new AsyncSelectWhereResult<TNode>(
        nodeAndPosition.Node,
        _Predicate(nodeAndPosition.Node, nodeAndPosition.Position)
          ? NodeTraversalStrategies.PruneSubtree
          : NodeTraversalStrategies.TraverseAll);
  }
}
