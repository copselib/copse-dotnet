using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneBefore's result (prune polarity: true = prune): the whole subtree goes.
  internal readonly struct PruneBeforeResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PruneBeforeResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.SkipNodeAndDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
