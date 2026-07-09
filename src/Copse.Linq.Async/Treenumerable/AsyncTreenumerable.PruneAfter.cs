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
    public static IAsyncTreenumerable<T> PruneAfter<T>(
      this IAsyncTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.Create(
          () => new AsyncPruneAfterTreenumerator<T>(source.GetAsyncBreadthFirstTreenumerator, predicate),
          () => new AsyncPruneAfterTreenumerator<T>(source.GetAsyncDepthFirstTreenumerator, predicate));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(source.GetAsyncDepthFirstTreenumerator, predicate));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(source.GetAsyncBreadthFirstTreenumerator, predicate));
    }
  }
}
