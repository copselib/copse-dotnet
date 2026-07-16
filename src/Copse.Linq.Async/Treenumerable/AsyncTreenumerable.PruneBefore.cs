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

      // A value predicate observes no coordinates, so it composes unconditionally.
      if (source is IAsyncFusableTreenumerable<T> fusableSource)
        return fusableSource.Map.Filter(
          nodeContext => new FusionVerdict<T>(
            nodeContext.Node,
            predicate(nodeContext.Node)
              ? NodeTraversalStrategies.SkipNodeAndDescendants
              : NodeTraversalStrategies.TraverseAll),
          relabels: true).ToTreenumerable();

      return new FusableTreenumerable<T, T, PruneBeforeVerdictSelector<T>>(
        source, new PruneBeforeVerdictSelector<T>(predicate), containsRelabelingStage: true);
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
      if (source is IAsyncFusableTreenumerable<T> fusableSource && !fusableSource.Map.ContainsRelabelingStage)
        return fusableSource.Map.Filter(
          nodeContext => new FusionVerdict<T>(
            nodeContext.Node,
            predicate(nodeContext.Node, nodeContext.Position)
              ? NodeTraversalStrategies.SkipNodeAndDescendants
              : NodeTraversalStrategies.TraverseAll),
          relabels: true).ToTreenumerable();

      return new FusableTreenumerable<T, T, PositionalPruneBeforeVerdictSelector<T>>(
        source, new PositionalPruneBeforeVerdictSelector<T>(predicate), containsRelabelingStage: true);
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T, PruneBeforeVerdictSelector<T>>(
            source.GetAsyncDepthFirstTreenumerator, new PruneBeforeVerdictSelector<T>(predicate)));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T, PositionalPruneBeforeVerdictSelector<T>>(
            source.GetAsyncDepthFirstTreenumerator, new PositionalPruneBeforeVerdictSelector<T>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T, PruneBeforeVerdictSelector<T>>(
            source.GetAsyncBreadthFirstTreenumerator, new PruneBeforeVerdictSelector<T>(predicate)));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T, PositionalPruneBeforeVerdictSelector<T>>(
            source.GetAsyncBreadthFirstTreenumerator, new PositionalPruneBeforeVerdictSelector<T>(predicate)));
    }
  }
}
