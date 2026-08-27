using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // PruneSiblingsWhere's result (prune polarity: true = prune): the matched node stays --
  // visits, descendants, and label untouched -- and its later siblings are never scheduled.
  // No surviving node's label moves, so the leg never relabels.
  internal readonly struct AsyncPruneSiblingsWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPruneSiblingsWhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeAndPosition<TNode> nodeAndPosition)
      => new AsyncSelectWhereResult<TNode>(
        nodeAndPosition.Node,
        _Predicate(nodeAndPosition.Node)
          ? NodeTraversalStrategies.PruneSiblings
          : NodeTraversalStrategies.TraverseAll);
  }
}
