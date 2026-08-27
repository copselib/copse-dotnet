using System;

namespace Copse.Linq.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows.
  partial class AsyncPruneDescendantsWhereTreenumerable<TNode>
  {
    /// <inheritdoc/>
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TNode, TOuterResult> selector)
    {
      return new AsyncSelectPruneDescendantsWhereTreenumerable<TNode, TOuterResult>(
        _Source, AsyncSelectWhereComposition.PruneDescendantsWhereThenSelect(_Predicate, nodeContext => selector(nodeContext.Node)));
    }

    /// <inheritdoc/>
    public IAsyncPruneDescendantsWhereTreenumerable<TNode> ComposePruneDescendantsWhere(Func<TNode, bool> predicate)
    {
      return new AsyncPruneDescendantsWhereTreenumerable<TNode>(
        _Source, AsyncSelectWhereComposition.PruneDescendantsWhereThenPruneDescendantsWhere(_Predicate, nodeContext => predicate(nodeContext.Node)));
    }
  }
}
