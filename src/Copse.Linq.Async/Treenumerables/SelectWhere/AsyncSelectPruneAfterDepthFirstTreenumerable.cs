using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // AsyncSelectPruneAfterTreenumerable's depth-first-only twin: the middle representation tier
  // -- a composed chain of projections and prune-afters over a single-dimension source, riding
  // the light passthrough driver.
  internal sealed class AsyncSelectPruneAfterDepthFirstTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterDepthFirstTreenumerable<TResult>
  {
    public AsyncSelectPruneAfterDepthFirstTreenumerable(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncDepthFirstTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, SelectWhereResult<TResult>> _ResultSelector;

    // The tier invariant: nothing in the chain moves a label.
    public bool Relabels => false;

    // A projection composes in-tier.
    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterDepthFirstTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenSelect(_ResultSelector, selector));
    }

    // A prune-after composes in-tier.
    public IAsyncDepthFirstTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterDepthFirstTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenPruneAfter(_ResultSelector, predicate));
    }

    // A rejecting operator arrived: convert to the general representation.
    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      return new SelectWhereDepthFirstTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          SelectWhereComposition.SelectPruneAfterThenResultSelector(_ResultSelector, resultSelector)),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);
  }
}
