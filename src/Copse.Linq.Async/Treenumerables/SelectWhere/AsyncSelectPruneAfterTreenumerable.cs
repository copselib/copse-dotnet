using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The middle representation tier: a composed chain of projections and prune-afters. Every
  // result preserves labels and never carries SkipNode, so the chain runs on the light
  // passthrough driver -- no promotion machinery, no path state, one driver class for both
  // dimensions. Only composition produces this wrapper (plain Select and plain PruneAfter
  // keep their own cheapest machinery), so the arrow is delegate-bound by nature and needs
  // no struct seam.
  internal sealed class AsyncSelectPruneAfterTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterTreenumerable<TResult>
  {
    public AsyncSelectPruneAfterTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, SelectWhereResult<TResult>> _ResultSelector;

    // A projection composes in-tier.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenSelect(_ResultSelector, selector));
    }

    // The seal experiment's donation: the chain's composed closure rides as one
    // FuncResultSelector leaf; the splice and the rejecting leg are structs.
    public IAsyncTreenumerable<TOuterResult> ComposeRejecting<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new SelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, FuncResultSelector<TSource, TResult>, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, FuncResultSelector<TSource, TResult>, TOuterSelector>(
          new FuncResultSelector<TSource, TResult>(_ResultSelector), outerSelector),
        relabels);
    }

    // A prune-after composes in-tier.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenPruneAfter(_ResultSelector, predicate));
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);
  }
}
