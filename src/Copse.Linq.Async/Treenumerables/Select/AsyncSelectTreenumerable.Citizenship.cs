using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows: the light tier's citizenship IS its
  // machinery.
  partial class AsyncSelectTreenumerable<TSource, TResult>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectThenSelect(_Selector, nodeContext => selector(nodeContext.Node)));
    }

    public IAsyncPruneAfterTreenumerable<TResult> ComposePruneAfter(Func<TResult, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult, ComposedResultSelector<TSource, TResult, TResult, SelectResultSelector<TSource, TResult>, PruneAfterResultSelector<TResult>>>(
        _Source,
        new ComposedResultSelector<TSource, TResult, TResult, SelectResultSelector<TSource, TResult>, PruneAfterResultSelector<TResult>>(
          new SelectResultSelector<TSource, TResult>(_Selector), new PruneAfterResultSelector<TResult>(nodeContext => predicate(nodeContext.Node))));
    }
  }
}
