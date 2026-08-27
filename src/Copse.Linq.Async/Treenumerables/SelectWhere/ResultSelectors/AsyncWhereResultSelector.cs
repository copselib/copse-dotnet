using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // Plain value-Where's result: keep when true, otherwise SkipNode (children promote).
  internal readonly struct AsyncWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncWhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new AsyncSelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
