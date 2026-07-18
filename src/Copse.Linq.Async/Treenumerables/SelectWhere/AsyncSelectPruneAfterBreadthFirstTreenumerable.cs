using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // AsyncSelectPruneAfterTreenumerable's breadth-first-only twin: the middle representation tier
  // -- a composed chain of projections and prune-afters over a single-dimension source, riding
  // the light passthrough driver.
  internal sealed class AsyncSelectPruneAfterBreadthFirstTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterBreadthFirstTreenumerable<TResult>
  {
    public AsyncSelectPruneAfterBreadthFirstTreenumerable(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncBreadthFirstTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, SelectWhereResult<TResult>> _ResultSelector;

    // The tier invariant: nothing in the chain moves a label.
    public bool Relabels => false;

    // A projection composes in-tier.
    public IAsyncBreadthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterBreadthFirstTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenSelect(_ResultSelector, selector));
    }

    // A prune-after composes in-tier.
    public IAsyncBreadthFirstTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterBreadthFirstTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenPruneAfter(_ResultSelector, predicate));
    }

    // A rejecting operator arrived: convert to the general representation.
    public IAsyncBreadthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      return new SelectWhereBreadthFirstTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          SelectWhereComposition.SelectPruneAfterThenResultSelector(_ResultSelector, resultSelector)),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);
  }
}
