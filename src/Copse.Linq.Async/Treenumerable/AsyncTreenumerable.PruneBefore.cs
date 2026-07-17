using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneBefore</c> over node VALUES (prune polarity: true = prune): removes each
    /// matching node AND its whole subtree. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<T> PruneBefore<T>(
      this IAsyncTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The stage
      // is the plain path's selector struct: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<T, T, PruneBeforeResultSelector<T>>(
        source, new PruneBeforeResultSelector<T>(predicate), containsRelabelingStage: true);
    }

    /// <summary>
    /// Async <c>PruneBefore</c> over (node, position) (prune polarity: true = prune). Deferred.
    /// Each positional predicate sees ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<T> PruneBefore<T>(
      this IAsyncTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.ContainsRelabelingStage)
        return selectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<T, T, PositionalPruneBeforeResultSelector<T>>(
        source, new PositionalPruneBeforeResultSelector<T>(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T, PruneBeforeResultSelector<T>>(
            source.GetAsyncDepthFirstTreenumerator, new PruneBeforeResultSelector<T>(predicate)));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T, PositionalPruneBeforeResultSelector<T>>(
            source.GetAsyncDepthFirstTreenumerator, new PositionalPruneBeforeResultSelector<T>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T, PruneBeforeResultSelector<T>>(
            source.GetAsyncBreadthFirstTreenumerator, new PruneBeforeResultSelector<T>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T, PositionalPruneBeforeResultSelector<T>>(
            source.GetAsyncBreadthFirstTreenumerator, new PositionalPruneBeforeResultSelector<T>(predicate)));
    }
  }
}
