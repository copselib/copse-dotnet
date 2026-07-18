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

      // The light tier composes a prune-after in-tier and keeps no-promotion machinery:
      // prune over prune merges predicates on the bespoke driver; prune over projections
      // rides the light passthrough driver.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      // A value predicate observes no coordinates, so it composes unconditionally. The selector
      // comes from the wrapper's CreateResultSelector: the operator's semantics, stated once.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node)),
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

      // The light tier composes a prune-after in-tier (see the value overload); the tier
      // never relabels, so the positional flavor always qualifies for the join rule.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      // The join rule: a positional predicate composes only over a label-preserving chain.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncPruneAfterTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<T> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node)),
          relabels: false);

      return new AsyncPruneAfterDepthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The light tier never relabels, so the positional flavor always qualifies for the join
      // rule; on the general representation it composes only over a label-preserving chain.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<T> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource && !depthFirstSelectWhereSource.Relabels)
        return depthFirstSelectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncPruneAfterDepthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<T> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node)),
          relabels: false);

      return new AsyncPruneAfterBreadthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<T> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource && !breadthFirstSelectWhereSource.Relabels)
        return breadthFirstSelectWhereSource.Compose(
          AsyncPruneAfterTreenumerable<T>.CreateResultSelector(nodeContext => predicate(nodeContext.Node, nodeContext.Position)),
          relabels: false);

      return new AsyncPruneAfterBreadthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }
  }
}
