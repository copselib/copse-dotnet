using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // Plain positional Where's result: the predicate sees (node, this layer's input labels).
  internal readonly struct PositionalWhereResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PositionalWhereResultSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public CompositionResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new CompositionResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
