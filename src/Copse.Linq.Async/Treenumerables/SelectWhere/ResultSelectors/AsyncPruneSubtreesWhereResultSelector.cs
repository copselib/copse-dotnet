using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // PruneSubtreesWhere's result (prune polarity: true = prune): the whole subtree goes.
  internal readonly struct AsyncPruneSubtreesWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPruneSubtreesWhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeAndPosition<TNode> nodeAndPosition)
      => new AsyncSelectWhereResult<TNode>(
        nodeAndPosition.Node,
        _Predicate(nodeAndPosition.Node)
          ? NodeTraversalStrategies.PruneSubtree
          : NodeTraversalStrategies.TraverseAll);
  }
}
