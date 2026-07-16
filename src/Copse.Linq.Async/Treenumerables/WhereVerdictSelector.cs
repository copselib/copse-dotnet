using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // Plain value-Where's verdict: keep when true, otherwise SkipNode (children promote).
  internal readonly struct WhereVerdictSelector<TNode> : IVerdictSelector<TNode, TNode>
  {
    public WhereVerdictSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public FusionVerdict<TNode> GetVerdict(NodeContext<TNode> nodeContext)
      => new FusionVerdict<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext.Node)
          ? NodeTraversalStrategies.TraverseAll
          : NodeTraversalStrategies.SkipNode);
  }
}
