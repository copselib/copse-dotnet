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
    /// Async <c>Select</c> over node VALUES: maps each node, forwarding the visit stream
    /// unchanged (positions never move under a projection). Deferred. Consecutive selects
    /// collapse by selector composition, and a following Where (either flavor) composes into
    /// the projection-carrying filter driver (docs/OPERATOR_COMPOSITION_DESIGN.md).
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // A value selector observes no coordinates, so it composes unconditionally. The fast
      // path first: a projection-only chain composes selectors and stays on the light
      // acquisition; anything else composes the projection as a never-rejecting selector.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return SelectCore(source, nodeContext => selector(nodeContext.Node));
    }

    /// <summary>
    /// Async <c>Select</c> over (node, position) -- the positional analog of LINQ's indexed
    /// Select. Positions never move under a projection, so this flavor composes exactly like
    /// the value-only one.
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule (see Where's positional overload): splice only over a label-preserving
      // chain; otherwise stack, so the selector reads genuinely emitted labels.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource && !selectPruneAfterSource.Relabels)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return SelectCore(source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }

    private static IAsyncTreenumerable<TResult> SelectCore<TSource, TResult>(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TResult>(source, selector);
    }

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // The narrow probes mirror the composite overload's. A composite-width wrapper arriving
      // through a narrow-typed receiver composes on its own representation -- the successor
      // keeps both dimensions; a narrow chain composes to a narrow successor.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<TSource> depthFirstSelectPruneAfterSource)
        return depthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource)
        return depthFirstSelectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node));
    }

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      // The join rule (see the composite positional overload): splice only over a
      // label-preserving chain.
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource && !selectPruneAfterSource.Relabels)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      if (source is IAsyncSelectPruneAfterDepthFirstTreenumerable<TSource> depthFirstSelectPruneAfterSource && !depthFirstSelectPruneAfterSource.Relabels)
        return depthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereDepthFirstTreenumerable<TSource> depthFirstSelectWhereSource && !depthFirstSelectWhereSource.Relabels)
        return depthFirstSelectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return new AsyncSelectDepthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<TSource> breadthFirstSelectPruneAfterSource)
        return breadthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource)
        return breadthFirstSelectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node));
    }

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
    {
      if (source is IAsyncSelectPruneAfterTreenumerable<TSource> selectPruneAfterSource && !selectPruneAfterSource.Relabels)
        return selectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereTreenumerable<TSource> selectWhereSource && !selectWhereSource.Relabels)
        return selectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      if (source is IAsyncSelectPruneAfterBreadthFirstTreenumerable<TSource> breadthFirstSelectPruneAfterSource && !breadthFirstSelectPruneAfterSource.Relabels)
        return breadthFirstSelectPruneAfterSource.Compose(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncSelectWhereBreadthFirstTreenumerable<TSource> breadthFirstSelectWhereSource && !breadthFirstSelectWhereSource.Relabels)
        return breadthFirstSelectWhereSource.Compose(
          nodeContext => new SelectWhereResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
          relabels: false);

      return new AsyncSelectBreadthFirstTreenumerable<TSource, TResult>(
        source, nodeContext => selector(nodeContext.Node, nodeContext.Position));
    }
  }
}
