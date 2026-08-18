using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The middle (light passthrough) tier, STRUCT-COMPOSED (attempt #2,
  // WITHPOSITION_DESIGN.md status -- GATE-FAILING, kept for review): the chain nests in
  // the TYPE -- ComposedResultSelector legs, user lambdas as leaves -- exactly as the
  // driver tier's chains have since the reunification. Measured WORSE than the closure
  // arrows under per-visit evaluation (59.4 -> 71.0 ms DFT, 55.7 -> 146.2 ms BFT on the
  // PositionalPruneAfter_Spelled witness); the revert is Jason's ruling.
  internal sealed partial class AsyncSelectPruneAfterTreenumerable<TSource, TResult, TResultSelector> : IAsyncSelectWhereTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public AsyncSelectPruneAfterTreenumerable(
      IAsyncTreenumerable<TSource> source,
      TResultSelector resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    // The general surface: light chains never relabel.
    public bool Relabels => false;

    // A projection composes in-tier: the leg nests in the type.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>(
          _ResultSelector, new SelectResultSelector<TResult, TOuterResult>(selector)));
    }

    // A prune-after composes in-tier: the leg nests in the type.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult, ComposedResultSelector<TSource, TResult, TResult, TResultSelector, PruneAfterResultSelector<TResult>>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TResult, TResultSelector, PruneAfterResultSelector<TResult>>(
          _ResultSelector, new PruneAfterResultSelector<TResult>(predicate)));
    }

    // The struct splice (the open seal): this chain rides the driver as its inner leg,
    // already a struct -- no closure leaf, no re-wrapping.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TResult, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, TOuterSelector>(_ResultSelector, outerSelector),
        relabels);
    }

    // The Func splice: the struct splice with the closure as its one leaf.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<TResult, TOuterResult>>(
        new FuncResultSelector<TResult, TOuterResult>(resultSelector), relabels);

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult, TResultSelector>(_Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult, TResultSelector>(_Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);
  }
}
