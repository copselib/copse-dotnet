using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows.
  partial class AsyncSelectPruneAfterTreenumerable<TSource, TResult>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenSelect(_ResultSelector, nodeContext => selector(nodeContext.Node)));
    }

    public IAsyncPruneAfterTreenumerable<TResult> ComposePruneAfter(Func<TResult, bool> predicate)
    {
      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectPruneAfterThenPruneAfter(_ResultSelector, nodeContext => predicate(nodeContext.Node)));
    }
  }
}
