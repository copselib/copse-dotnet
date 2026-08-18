using Copse.Core;
using System.Runtime.CompilerServices;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Linq.Async.Treenumerables
{
  // A prune-after as a result-selector leg: keep the node, shed its subtree on a match --
  // never SkipNode, so it stays a light-tier fact even spliced into the general driver.
  internal readonly struct PruneAfterResultSelector<TNode> : IResultSelector<TNode, TNode>
  {
    public PruneAfterResultSelector(Func<NodeContext<TNode>, bool> predicate)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SelectWhereResult<TNode> GetResult(NodeContext<TNode> nodeContext)
      => new SelectWhereResult<TNode>(
        nodeContext.Node,
        _Predicate(nodeContext)
          ? NodeTraversalStrategies.SkipDescendants
          : NodeTraversalStrategies.TraverseAll);
  }
}
