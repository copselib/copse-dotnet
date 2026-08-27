using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // A prune-after as a result-selector leg: keep the node, shed its subtree on a match --
  // never SkipNode, so it stays a light-tier fact even spliced into the general driver.
  internal readonly struct AsyncPruneDescendantsWhereResultSelector<TNode> : IAsyncResultSelector<TNode, TNode>
  {
    public AsyncPruneDescendantsWhereResultSelector(Func<NodeAndPosition<TNode>, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeAndPosition<TNode>, bool> _Predicate;

    public AsyncSelectWhereResult<TNode> GetResult(NodeAndPosition<TNode> nodeAndPosition)
      => new AsyncSelectWhereResult<TNode>(
        nodeAndPosition.Node,
        _Predicate(nodeAndPosition)
          ? NodeTraversalStrategies.PruneDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
