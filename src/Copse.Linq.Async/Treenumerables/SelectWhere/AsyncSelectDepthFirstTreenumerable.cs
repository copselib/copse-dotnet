using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // AsyncSelectTreenumerable's depth-first-only twin: the pure-projection wrapper over a
  // source that affords just that dimension. Projection-only chains keep the light
  // AsyncSelectTreenumerator acquisition; the representation choices mirror the wide twin's.
  internal sealed class AsyncSelectDepthFirstTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterDepthFirstTreenumerable<TResult>
  {
    public AsyncSelectDepthFirstTreenumerable(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IAsyncDepthFirstTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    // Projections never relabel.
    public bool Relabels => false;

    // The fast path: a projection composed onto a projection is still a projection.
    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectDepthFirstTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectThenSelect(_Selector, selector));
    }

    // A prune-after joins: promote to the middle tier, never the filter driver.
    public IAsyncDepthFirstTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterDepthFirstTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectThenPruneAfter(_Selector, predicate));
    }

    // The general Compose converts the representation.
    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      return new SelectWhereDepthFirstTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          SelectWhereComposition.SelectThenResultSelector(_Selector, resultSelector)),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);
  }
}
