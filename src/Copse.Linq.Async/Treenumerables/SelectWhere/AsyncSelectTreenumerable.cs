using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The pure-projection wrapper. Kept distinct from AsyncSelectWhereTreenumerable deliberately: a chain of
  // nothing but Selects acquires through the light AsyncSelectTreenumerator, not the filter
  // driver -- plain operators keep their cheapest machinery; the general driver is paid only
  // when a rejecting operator joins (the representation choice IS the type split).
  // Since the seal opened (2026-08-18) every light wrapper is on the general-splice surface
  // through the tier interface's inheritance; this wrapper was the first (dual citizenship,
  // 2026-08-04) because absorbing a full projection layer was the family's measured win.
  internal sealed partial class AsyncSelectTreenumerable<TSource, TResult> : IAsyncSelectWhereTreenumerable<TResult>
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

    // Projections never relabel.
    public bool Relabels => false;

    // The fast path: a projection composed onto a projection is still a projection, so the
    // chain keeps the light acquisition.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectThenSelect(_Selector, selector));
    }

    // A prune-after joins: promote to the middle tier (light passthrough driver), never the
    // filter driver.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectThenPruneAfter(_Selector, predicate));
    }

    // The Func splice: the struct splice with the closure as its one leaf.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<TResult, TOuterResult>>(
        new FuncResultSelector<TResult, TOuterResult>(resultSelector), relabels);

    // The struct-composed form -- THE LIGHT TIER DONATING A STRUCT LEG (the reunification
    // gate's decisive case: the tier seal existed because this wrapper's pieces used to
    // arrive as bare Funcs and de-inlined the whole splice chain; here its projection rides
    // an inlinable struct leg, the user lambda staying a leaf call).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, SelectResultSelector<TSource, TResult>, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, SelectResultSelector<TSource, TResult>, TOuterSelector>(
          new SelectResultSelector<TSource, TResult>(_Selector), outerSelector),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);
  }
}
