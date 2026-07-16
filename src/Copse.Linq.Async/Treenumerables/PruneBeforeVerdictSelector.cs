using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneBefore's verdict (prune polarity: true = prune): the whole subtree goes.
  internal readonly struct PruneBeforeVerdictSelector<TNode> : IVerdictSelector<TNode, TNode>
  {
    public PruneBeforeVerdictSelector(Func<TNode, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<TNode, bool> _Predicate;

    public FusionVerdict<TNode> GetVerdict(NodeContext<TNode> nodeContext)
      => _Predicate(nodeContext.Node)
        ? FusionVerdict<TNode>.Reject(NodeTraversalStrategies.SkipNodeAndDescendants)
        : FusionVerdict<TNode>.Accept(nodeContext.Node);
  }
}
