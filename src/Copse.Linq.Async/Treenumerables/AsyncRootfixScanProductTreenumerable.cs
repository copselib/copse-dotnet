using Copse.Linq.Treenumerators;
using Copse.Core;
using Copse.Linq; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Treenumerables
{
  // A composed-projection variant of a rootfix scan (the streaming projection citizenship):
  // the same bare recipe as the plain citizen, with the product selector planted inside the
  // product engines -- one selector call at emission over a transient pairing, instead of a
  // wrapper layer per pull. Itself a citizen: further Selects compose onto the selector
  // (closure by signature).
  internal sealed class AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TProduct>
    : IAsyncSelectTreenumerable<TProduct>,
      IAsyncSelectWhereTreenumerable<TProduct>
  {
    public AsyncRootfixScanProductTreenumerable(
      Func<IAsyncTreenumerator<TNode>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TNode>> innerBreadthFirstFactory,
      Func<NodeAndPosition<TAccumulate>, NodeAndPosition<TNode>, TAccumulate> accumulator,
      TAccumulate seed,
      Func<NodeAndPosition<NodeAccumulation<TNode, TAccumulate>>, TProduct> productSelector)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
      _ProductSelector = productSelector;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _InnerBreadthFirstFactory;
    private readonly Func<NodeAndPosition<TAccumulate>, NodeAndPosition<TNode>, TAccumulate> _Accumulator;
    private readonly TAccumulate _Seed;
    // Context-shaped by the rootfix door: the door surrenders
    // NodeAndPosition-shaped projectors; value-shaped composition wraps at the seams below.
    private readonly Func<NodeAndPosition<NodeAccumulation<TNode, TAccumulate>>, TProduct> _ProductSelector;

    public IAsyncTreenumerator<TProduct> GetAsyncDepthFirstTreenumerator()
      => new AsyncRootfixScanProductDepthFirstTreenumerator<TNode, TAccumulate, TProduct>(
        _InnerDepthFirstFactory, _Accumulator, _Seed, _ProductSelector);

    public IAsyncTreenumerator<TProduct> GetAsyncBreadthFirstTreenumerator()
      => new AsyncRootfixScanProductBreadthFirstTreenumerator<TNode, TAccumulate, TProduct>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed, _ProductSelector);

    public IAsyncSelectTreenumerable<TResult> ComposeSelect<TResult>(Func<TProduct, TResult> selector)
    {
      var currentProductSelector = _ProductSelector;

      return new AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TResult>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed,
        pairingContext => selector(currentProductSelector(pairingContext)));
    }

    // The fourth-cell door (see the plain citizen): the composed product rides as a
    // AsyncSelectResultSelector inner leg, the splicing operator's leg composes over it, and the
    // whole chain -- fold, projection, rejection -- is ONE machine.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IAsyncResultSelector<TProduct, TOuterResult>
    {
      var currentProductSelector = _ProductSelector;

      return new AsyncScanWhereTreenumerable<TNode, TAccumulate, TOuterResult, AsyncComposedResultSelector<NodeAccumulation<TNode, TAccumulate>, TProduct, TOuterResult, AsyncSelectResultSelector<NodeAccumulation<TNode, TAccumulate>, TProduct>, TOuterSelector>>(
        _InnerDepthFirstFactory,
        _InnerBreadthFirstFactory,
        _Accumulator,
        _Seed,
        new AsyncComposedResultSelector<NodeAccumulation<TNode, TAccumulate>, TProduct, TOuterResult, AsyncSelectResultSelector<NodeAccumulation<TNode, TAccumulate>, TProduct>, TOuterSelector>(
          new AsyncSelectResultSelector<NodeAccumulation<TNode, TAccumulate>, TProduct>(currentProductSelector),
          outerSelector),
        relabels);
    }

    // The context-shaped projection door (a positional leg, join-rule-cleared by the
    // caller): the leg lands in the fold-carrying driver, as every splicing leg does here.
    // Never moves a label, so the position-reading doors ARE the blind doors.
    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult>(Func<NodeAndPosition<TProduct>, TOuterResult> selector)
      => Compose(selector);

    public IAsyncTreenumerable<TOuterResult> ComposePositional<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IAsyncResultSelector<TProduct, TOuterResult>
      => Compose<TOuterResult, TOuterSelector>(outerSelector, relabels);
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeAndPosition<TProduct>, TOuterResult> selector)
      => Compose<TOuterResult, AsyncSelectResultSelector<TProduct, TOuterResult>>(
        new AsyncSelectResultSelector<TProduct, TOuterResult>(selector), relabels: false);

    // The prune-after doors: the in-tier-only boundary ruling -- the light prune wrapper
    // stacks over the product citizen.
    public IAsyncTreenumerable<TProduct> ComposePruneDescendantsWhere(Func<NodeAndPosition<TProduct>, bool> predicate)
      => new AsyncPruneDescendantsWhereTreenumerable<TProduct>(this, predicate);

    public IAsyncPruneDescendantsWhereTreenumerable<TProduct> ComposePruneDescendantsWhere(Func<TProduct, bool> predicate)
      => new AsyncPruneDescendantsWhereTreenumerable<TProduct>(this, nodeAndPosition => predicate(nodeAndPosition.Node));
  }
}
