using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of the in-tier struct nesting.
  partial class AsyncSelectPruneAfterTreenumerable<TSource, TResult, TResultSelector>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TOuterResult, ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TOuterResult, TResultSelector, SelectResultSelector<TResult, TOuterResult>>(
          _ResultSelector, new SelectResultSelector<TResult, TOuterResult>(nodeContext => selector(nodeContext.Node))));
    }

    public IAsyncPruneAfterTreenumerable<TResult> ComposePruneAfter(Func<TResult, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult, ComposedResultSelector<TSource, TResult, TResult, TResultSelector, PruneAfterResultSelector<TResult>>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TResult, TResultSelector, PruneAfterResultSelector<TResult>>(
          _ResultSelector, new PruneAfterResultSelector<TResult>(nodeContext => predicate(nodeContext.Node))));
    }
  }
}
