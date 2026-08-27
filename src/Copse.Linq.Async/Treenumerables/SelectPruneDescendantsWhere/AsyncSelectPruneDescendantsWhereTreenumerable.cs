using Copse.Linq.Treenumerators;
using Copse.Core;
using Copse.Linq;
using System;

namespace Copse.Linq.Treenumerables
{
  // The middle representation tier: a composed chain of projections and prune-afters. Every
  // result preserves labels and never carries SkipNode, so the chain runs on the light
  // passthrough driver -- no promotion machinery, no path state, one driver class for both
  // dimensions. Only composition produces this wrapper (plain Select and plain PruneDescendantsWhere
  // keep their own cheapest machinery), so its IN-TIER arrow is delegate-bound by nature;
  // when a rejecting operator splices over it, its chain rides as one AsyncFuncResultSelector
  // leaf under struct plumbing.
  internal sealed partial class AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TResult> : IAsyncSelectWhereTreenumerable<TResult>
  {
    public AsyncSelectPruneDescendantsWhereTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, AsyncSelectWhereResult<TResult>> resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, AsyncSelectWhereResult<TResult>> _ResultSelector;

    // This tier never moves a label, so the position-reading doors ARE the blind doors.
    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
      => Compose(selector);

    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IAsyncResultSelector<TResult, TOuterResult>
      => Compose<TOuterResult, TOuterSelector>(outerSelector, relabels);
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TOuterResult>(
        _Source, AsyncSelectWhereComposition.SelectPruneDescendantsWhereThenSelect(_ResultSelector, selector));
    }

    // A rejecting operator splices over this chain: the composed closure rides as one
    // AsyncFuncResultSelector leaf; the splice plumbing and the outer leg are structs.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IAsyncResultSelector<TResult, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, AsyncComposedResultSelector<TSource, TResult, TOuterResult, AsyncFuncResultSelector<TSource, TResult>, TOuterSelector>>(
        _Source,
        new AsyncComposedResultSelector<TSource, TResult, TOuterResult, AsyncFuncResultSelector<TSource, TResult>, TOuterSelector>(
          new AsyncFuncResultSelector<TSource, TResult>(_ResultSelector), outerSelector));
    }

    // A prune-after composes in-tier.
    public IAsyncTreenumerable<TResult> ComposePruneDescendantsWhere(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TResult>(
        _Source, AsyncSelectWhereComposition.SelectPruneDescendantsWhereThenPruneDescendantsWhere(_ResultSelector, predicate));
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectPruneDescendantsWhereTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectPruneDescendantsWhereTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);
  }
}
