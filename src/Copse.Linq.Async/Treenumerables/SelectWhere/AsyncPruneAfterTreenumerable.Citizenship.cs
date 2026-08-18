using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). Both doors are the value
  // flavors of this wrapper's in-tier arrows.
  partial class AsyncPruneAfterTreenumerable<TNode>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TNode, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TNode, TOuterResult>(
        _Source, SelectWhereComposition.PruneAfterThenSelect(_Predicate, nodeContext => selector(nodeContext.Node)));
    }

    public IAsyncPruneAfterTreenumerable<TNode> ComposePruneAfter(Func<TNode, bool> predicate)
    {
      return new AsyncPruneAfterTreenumerable<TNode>(
        _Source, SelectWhereComposition.PruneAfterThenPruneAfter(_Predicate, nodeContext => predicate(nodeContext.Node)));
    }
  }
}
