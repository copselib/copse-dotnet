using Copse.Core;
using Copse.Core.Async;
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

      // ONE sniff (PUBLIC_COMPOSITION_SURFACE_DESIGN.md): every member's ComposePruneAfter
      // is that member's best machinery under the in-tier-only boundary ruling (2026-08-04,
      // the surviving half) -- the light tier merges in-tier and keeps no-promotion
      // machinery; every other internal member STACKS the light wrapper over itself, since
      // joining would demote its representation for a layer that costs almost nothing;
      // foreign citizens absorb into their recipe.
      if (source is IAsyncPruneAfterTreenumerable<T> pruneComposableSource)
        return pruneComposableSource.ComposePruneAfter(predicate);

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

      // The positional flavor takes the wrapper over citizens (the value-only contract
      // rule); the internal context-shaped door dispatches per member -- the light tier
      // merges in-tier (it never relabels, so the positional flavor always qualifies),
      // everyone else stacks. Stacking preserves the join rule by construction: the
      // stacked predicate reads its input tree's emitted labels.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      return new AsyncPruneAfterTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The narrow probes mirror the composite overload's (the context-shaped door
      // dispatches per member). A composite-width wrapper arriving through a narrow-typed
      // receiver composes on its own representation -- the successor keeps both dimensions;
      // a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      return new AsyncPruneAfterDepthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      // The light tier never relabels, so the positional flavor always qualifies for the
      // join rule; stacked members preserve it by construction.
      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<T> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      return new AsyncPruneAfterDepthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node));

      return new AsyncPruneAfterBreadthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneAfter<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<T, NodePosition, bool> predicate)
    {
      if (predicate == null)
        return source;

      if (source is IAsyncSelectWhereTreenumerable<T> selectWhereSource)
        return selectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<T> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.ComposePruneAfter(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

      return new AsyncPruneAfterBreadthFirstTreenumerable<T>(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position));
    }
  }
}
