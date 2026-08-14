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

      // A value predicate observes no coordinates, so it composes unconditionally. The selector
      // is the plain path's struct: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<T, T, PruneBeforeResultSelector<T>>(
        source, new PruneBeforeResultSelector<T>(predicate), relabels: true);
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
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereTreenumerable<T, T, PositionalPruneBeforeResultSelector<T>>(
        source, new PositionalPruneBeforeResultSelector<T>(predicate), relabels: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereDepthFirstTreenumerable<T, T, PruneBeforeResultSelector<T>>(
        source, new PruneBeforeResultSelector<T>(predicate), relabels: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule, as in the composite positional overload: splice only while the chain
      // is label-preserving.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource && !depthFirstSelectWhereSource.Relabels)
        return depthFirstSelectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereDepthFirstTreenumerable<T, T, PositionalPruneBeforeResultSelector<T>>(
        source, new PositionalPruneBeforeResultSelector<T>(predicate), relabels: true);
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose(
          new PruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereBreadthFirstTreenumerable<T, T, PruneBeforeResultSelector<T>>(
        source, new PruneBeforeResultSelector<T>(predicate), relabels: true);
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource && !breadthFirstSelectWhereSource.Relabels)
        return breadthFirstSelectWhereSource.Compose(
          new PositionalPruneBeforeResultSelector<T>(predicate).GetResult, relabels: true);

      return new SelectWhereBreadthFirstTreenumerable<T, T, PositionalPruneBeforeResultSelector<T>>(
        source, new PositionalPruneBeforeResultSelector<T>(predicate), relabels: true);
    }
  }
}
