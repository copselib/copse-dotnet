using System;

namespace Copse.Linq.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows: the light tier's citizenship IS its
  // machinery.
  partial class AsyncSelectTreenumerable<TSource, TResult>
  {
    /// <inheritdoc/>
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source, AsyncSelectWhereComposition.SelectThenSelect(_Selector, nodeAndPosition => selector(nodeAndPosition.Node)));
    }

    /// <inheritdoc/>
    public IAsyncPruneDescendantsWhereTreenumerable<TResult> ComposePruneDescendantsWhere(Func<TResult, bool> predicate)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TSource, TResult>(
        _Source, AsyncSelectWhereComposition.SelectThenPruneDescendantsWhere(_Selector, nodeAndPosition => predicate(nodeAndPosition.Node)));
    }
  }
}
