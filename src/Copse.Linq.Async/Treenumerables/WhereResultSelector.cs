using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // Plain value-Where's result: keep when true, otherwise SkipNode (children promote).
  internal readonly struct WhereResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public WhereResultSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public CompositionResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new CompositionResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
