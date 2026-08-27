using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // A prune-after as a result-selector leg: keep the node, shed its subtree on a match --
  // never SkipNode, so it stays a light-tier fact even spliced into the general driver.
  internal readonly struct AsyncPruneDescendantsWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPruneDescendantsWhereResultSelector(Func<NodeContext<TNode>, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new AsyncSelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext)
          ? NodeTraversalStrategies.PruneDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
