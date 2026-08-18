using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The positional PruneBefore's result: the predicate sees (node, this layer's input labels).
  internal readonly struct PositionalPruneBeforeResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PositionalPruneBeforeResultSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.SkipNodeAndDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
