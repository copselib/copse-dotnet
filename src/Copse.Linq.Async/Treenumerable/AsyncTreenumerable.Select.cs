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
    /// the projection-carrying filter driver (docs/OPERATOR_FUSION_DESIGN.md).
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
    {
      // A value selector observes no coordinates, so it composes unconditionally. The fast
      // path first: a projection-only chain composes selectors and stays on the light
      // acquisition; anything else composes the projection as a never-rejecting stage.
      if (source is IAsyncComposableProjection<TSource> projectionSource)
        return projectionSource.ComposeProjection(nodeContext => selector(nodeContext.Node));

      if (source is IAsyncComposableTreenumerable<TSource> composableSource)
        return composableSource.Compose(
          nodeContext => new CompositionResult<TResult>(selector(nodeContext.Node), NodeTraversalStrategies.TraverseAll),
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
      if (source is IAsyncComposableProjection<TSource> projectionSource && !projectionSource.ContainsRelabelingStage)
        return projectionSource.ComposeProjection(nodeContext => selector(nodeContext.Node, nodeContext.Position));

      if (source is IAsyncComposableTreenumerable<TSource> composableSource && !composableSource.ContainsRelabelingStage)
        return composableSource.Compose(
          nodeContext => new CompositionResult<TResult>(selector(nodeContext.Node, nodeContext.Position), NodeTraversalStrategies.TraverseAll),
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
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(
          source.GetAsyncDepthFirstTreenumerator, nodeContext => selector(nodeContext.Node)));

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(
          source.GetAsyncDepthFirstTreenumerator, nodeContext => selector(nodeContext.Node, nodeContext.Position)));

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(
          source.GetAsyncBreadthFirstTreenumerator, nodeContext => selector(nodeContext.Node)));

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(
          source.GetAsyncBreadthFirstTreenumerator, nodeContext => selector(nodeContext.Node, nodeContext.Position)));
  }
}
