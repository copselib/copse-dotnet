using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows.
  partial class AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TResult>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TOuterResult>(
        _Source, SelectWhereComposition.SelectPruneDescendantsWhereThenSelect(_ResultSelector, nodeContext => selector(nodeContext.Node)));
    }

    public IAsyncPruneDescendantsWhereTreenumerable<TResult> ComposePruneDescendantsWhere(Func<TResult, bool> predicate)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TResult>(
        _Source, SelectWhereComposition.SelectPruneDescendantsWhereThenPruneDescendantsWhere(_ResultSelector, nodeContext => predicate(nodeContext.Node)));
    }
  }
}
