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
    /// Async <c>PruneAfter</c> over node VALUES: keeps each matching node but sheds its subtree
    /// (the matched node is the deepest of its lineage kept). Deferred. PruneAfter is
    /// label-preserving: survivors keep their coordinates.
    /// </summary>
    public static IAsyncTreenumerable<T> PruneAfter<T>(
      this IAsyncTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // A value predicate observes no coordinates, so it composes unconditionally. The stage
      // comes from the wrapper's CreateStage: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateStage(nodeContext => predicate(nodeContext.Node)),
          relabels: false);

      return new AsyncPruneAfterTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    /// <summary>
    /// Async <c>PruneAfter</c> over (node, position). Deferred. The positional predicate sees
    /// ITS input tree's labels.
    /// </summary>
    public static IAsyncTreenumerable<T> PruneAfter<T>(
      this IAsyncTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.ContainsRelabelingStage)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateStage(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncPruneAfterTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(
            source.GetAsyncDepthFirstTreenumerator, nodeContext => predicate(nodeContext.Node)));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(
            source.GetAsyncDepthFirstTreenumerator, nodeContext => predicate(nodeContext.Node, nodeContext.Position)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(
            source.GetAsyncBreadthFirstTreenumerator, nodeContext => predicate(nodeContext.Node)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncPruneAfterTreenumerator<T>(
            source.GetAsyncBreadthFirstTreenumerator, nodeContext => predicate(nodeContext.Node, nodeContext.Position)));
    }
  }
}
