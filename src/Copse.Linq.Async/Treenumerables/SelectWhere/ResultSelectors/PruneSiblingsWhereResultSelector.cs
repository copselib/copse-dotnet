using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneSiblingsWhere's result (prune polarity: true = prune): the matched node stays --
  // visits, descendants, and label untouched -- and its later siblings are never scheduled.
  // No surviving node's label moves, so the leg never relabels.
  internal readonly struct PruneSiblingsWhereResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PruneSiblingsWhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.PruneSiblings
          : NodeTraversalStrategies.TraverseAll);
  }
}
