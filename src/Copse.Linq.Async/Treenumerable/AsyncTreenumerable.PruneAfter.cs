using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneAfter</c>: keeps each node that matches the predicate but sheds its subtree (the
    /// matched node is the deepest of its lineage kept). Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneAfter<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncPruneAfterTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, predicate),
        () => new AsyncPruneAfterTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, predicate));
  }
}
