using System;

namespace Copse.Linq.Treenumerables
{
  // The public doors (PUBLIC_COMPOSITION_SURFACE_DESIGN.md), in their own partial part
  // because the CompositeToNarrow fan-out is file-granular and the narrow twins must not
  // claim composite-width doors (narrow parity is deferred). The driver's doors are its
  // best machinery per the door-optimality law: a projection nests as a struct leg onto
  // the composed chain; a prune-after stacks the light wrapper (the in-tier-only boundary
  // ruling).
  partial class AsyncSelectWhereTreenumerable<TSource, TResult, TResultSelector>
  {
    public IAsyncSelectTreenumerable<TOuterResult> ComposeSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
      => Splice<TOuterResult, AsyncSelectResultSelector<TResult, TOuterResult>>(
        new AsyncSelectResultSelector<TResult, TOuterResult>(nodeAndPosition => selector(nodeAndPosition.Node)));

    public IAsyncPruneDescendantsWhereTreenumerable<TResult> ComposePruneDescendantsWhere(Func<TResult, bool> predicate)
      => new AsyncPruneDescendantsWhereTreenumerable<TResult>(this, nodeAndPosition => predicate(nodeAndPosition.Node));
  }
}
