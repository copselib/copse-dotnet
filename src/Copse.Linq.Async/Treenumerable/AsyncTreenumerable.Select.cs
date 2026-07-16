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
    /// unchanged (positions never move under a projection). Deferred. Consecutive selects fuse
    /// by selector composition, and a following Where (either flavor) fuses into the
    /// projection-carrying filter driver (docs/OPERATOR_FUSION_DESIGN.md).
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
      => SelectCore(source, nodeContext => selector(nodeContext.Node));

    /// <summary>
    /// Async <c>Select</c> over (node, position) -- the positional analog of LINQ's indexed
    /// Select. Positions never move under a projection, so this flavor fuses exactly like the
    /// value-only one.
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TResult> selector)
      => SelectCore(source, nodeContext => selector(nodeContext.Node, nodeContext.Position));

    private static IAsyncTreenumerable<TResult> SelectCore<TSource, TResult>(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      if (source is IAsyncFusableTreenumerable<TSource> fusableSource)
      {
        var fused = fusableSource.FuseSelect(selector);

        if (fused != null)
          return fused;
      }

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
