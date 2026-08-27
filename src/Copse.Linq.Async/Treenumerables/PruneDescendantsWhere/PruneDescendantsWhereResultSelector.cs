using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // A prune-after as a result-selector leg: keep the node, shed its subtree on a match --
  // never SkipNode, so it stays a light-tier fact even spliced into the general driver.
  internal readonly struct PruneDescendantsWhereResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PruneDescendantsWhereResultSelector(Func<NodeContext<TNode>, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext)
          ? NodeTraversalStrategies.PruneDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
