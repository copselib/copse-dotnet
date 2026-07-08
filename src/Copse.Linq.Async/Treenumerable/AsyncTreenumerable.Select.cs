using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Select</c>: maps each node's context, forwarding the visit stream unchanged.
    /// Deferred. Takes the selector over <see cref="NodeContext{TSource}"/> like the sync twin
    /// (position-aware selection is the tree-shaped contract; a value-only selector is
    /// <c>nc =&gt; f(nc.Node)</c>).
    /// </summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
      => new AsyncDelegatingTreenumerable<TResult>(
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncBreadthFirstTreenumerator, selector),
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncDepthFirstTreenumerator, selector));
  }
}
