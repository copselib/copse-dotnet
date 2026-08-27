using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneSubtreesWhere's result (prune polarity: true = prune): the whole subtree goes.
  internal readonly struct PruneSubtreesWhereResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PruneSubtreesWhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.PruneSubtree
          : NodeTraversalStrategies.TraverseAll);
  }
}
