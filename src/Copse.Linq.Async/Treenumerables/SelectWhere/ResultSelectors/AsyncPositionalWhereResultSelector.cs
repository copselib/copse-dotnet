using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // Plain positional Where's result: the predicate sees (node, this layer's input labels).
  internal readonly struct AsyncPositionalWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPositionalWhereResultSelector(Func<TNode, NodePosition, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, NodePosition, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new AsyncSelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node, nodeContext.Position)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
