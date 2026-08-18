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
      // rides the light passthrough driver. Prune-afters compose only in-tier -- the
      // SURVIVING half of the 2026-08-04 ruling (the other half, rejecting-over-light,
      // opened 2026-08-18): over a general chain the light wrapper stacks on top, since
      // joining would demote its representation for a layer that costs almost nothing.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

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

      // The light tier composes a prune-after in-tier (see the value overload, including the
      // in-tier-only boundary ruling); the tier never relabels, so the positional flavor
      // always qualifies for the join rule.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      return new AsyncPruneAfterTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's (in-tier only -- see the composite
      // value overload's boundary ruling). A composite-width wrapper arriving through a
      // narrow-typed receiver composes on its own representation -- the successor keeps both
      // dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<T> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      return new AsyncPruneAfterDepthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The light tier never relabels, so the positional flavor always qualifies for the join
      // rule (in-tier only -- see the composite value overload's boundary ruling).
      if (source is IAsyncSelectPruneAfterTreenumerable<T> selectPruneAfterSource)
        return selectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<T> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

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

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<T> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

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

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<T> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      return new AsyncPruneAfterBreadthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }
  }
}
