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

    public AsyncSelectWhereResult<TNode> GetResult(NodeAndPosition<TNode> nodeAndPosition)
      => new AsyncSelectWhereResult<TNode>(
        nodeAndPosition.Node,
        _Predicate(nodeAndPosition.Node)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
