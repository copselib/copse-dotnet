using Copse.Async;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Async <c>Select</c>: maps each node's value, forwarding the visit stream unchanged. Deferred.</summary>
    public static IAsyncTreenumerable<TResult> Select<TSource, TResult>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TResult> selector)
      => new AsyncDelegatingTreenumerable<TResult>(
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncBreadthFirstTreenumerator(), selector),
        () => new AsyncSelectTreenumerator<TSource, TResult>(source.GetAsyncDepthFirstTreenumerator(), selector));
  }
}
