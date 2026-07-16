using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneAfter's named wrapper: plain acquisition keeps the bespoke prune-after driver (no
  // promotion machinery -- it only ever sheds whole subtrees below kept nodes), and composability
  // costs one property: PruneAfter is label-preserving (survivors keep their coordinates), so
  // its map carries relabeling: false and even positional lambdas may compose across it.
  internal sealed class AsyncPruneAfterTreenumerable<TNode> : IAsyncComposableTreenumerable<TNode>
  {
    public AsyncPruneAfterTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    public ICompositionMap<TNode> Map
    {
      get
      {
        var predicate = _Predicate;

        return CompositionMap<TNode, TNode>.OfVerdict(
          _Source,
          nodeContext => new CompositionVerdict<TNode>(
            nodeContext.Node,
            predicate(nodeContext)
              ? NodeTraversalStrategies.SkipDescendants
              : NodeTraversalStrategies.TraverseAll),
          containsRelabelingStage: false);
      }
    }

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
