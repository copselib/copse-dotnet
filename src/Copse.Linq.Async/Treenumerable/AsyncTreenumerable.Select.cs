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
    /// Async <c>Select</c>: maps each node's context, forwarding the visit stream unchanged.
    /// Deferred. Takes the selector over <see cref="NodeContext{TSource}"/> like the sync twin
    /// (position-aware selection is the tree-shaped contract; a value-only selector is
    /// <c>nc =&gt; f(nc.Node)</c>). Consecutive selects fuse: selecting over a select composes the
    /// selectors instead of stacking wrappers.
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      if (source is IAsyncSelectTreenumerable<TSource> innerSelectTreenumerable)
        return innerSelectTreenumerable.Compose(selector);
      else
        return new AsyncSelectTreenumerable<TSource, TResult>(source, selector);
    }

    public static IAsyncDepthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncDepthFirstTreenumerator, selector));

    public static IAsyncBreadthFirstTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncBreadthFirstTreenumerator, selector));
  }
}
