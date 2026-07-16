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

      // A value predicate observes no coordinates, so it composes unconditionally.
      if (source is IAsyncFusableTreenumerable<T> fusableSource)
        return fusableSource.Map.Filter(
          nodeContext => predicate(nodeContext.Node)
            ? FusionVerdict<T>.Accept(nodeContext.Node, NodeTraversalStrategies.SkipDescendants)
            : FusionVerdict<T>.Accept(nodeContext.Node),
          relabels: false).ToTreenumerable();

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
      if (source is IAsyncFusableTreenumerable<T> fusableSource && !fusableSource.Map.ContainsRelabelingStage)
        return fusableSource.Map.Filter(
          nodeContext => predicate(nodeContext.Node, nodeContext.Position)
            ? FusionVerdict<T>.Accept(nodeContext.Node, NodeTraversalStrategies.SkipDescendants)
            : FusionVerdict<T>.Accept(nodeContext.Node),
          relabels: false).ToTreenumerable();

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
