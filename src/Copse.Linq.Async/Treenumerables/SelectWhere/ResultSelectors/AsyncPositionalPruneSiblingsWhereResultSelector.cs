using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // The positional PruneSiblingsWhere's result: the predicate sees (node, this layer's input
  // labels). Like the value form, the leg never relabels -- the matched node stays and only
  // its later siblings go.
  internal readonly struct AsyncPositionalPruneSiblingsWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPositionalPruneSiblingsWhereResultSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new AsyncSelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.PruneSiblings
          : NodeTraversalStrategies.TraverseAll);
  }
}
