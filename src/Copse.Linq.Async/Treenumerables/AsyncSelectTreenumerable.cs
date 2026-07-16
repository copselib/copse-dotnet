using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The pure-projection wrapper. Kept distinct from FusableTreenumerable deliberately: a chain of
  // nothing but Selects acquires through the light AsyncSelectTreenumerator, not the filter
  // driver -- plain operators keep their cheapest machinery; the general driver is paid only
  // when a filter joins (the map makes that representation choice at reification).
  internal sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncFusableTreenumerable<TResult>
  {
    public AsyncSelectTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    // Offer up the internal mapping: a projection-only map, so composition keeps the light
    // representation until a filter joins.
    public IFusionMap<TResult> Map => FusionMap<TSource, TResult>.OfProjection(_Source, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);


  }
}
