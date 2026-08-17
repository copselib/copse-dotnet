using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq.Async
{
  // A composed-projection variant of a rootfix scan (the streaming projection citizenship):
  // the same bare recipe as the plain citizen, with the product selector planted inside the
  // product engines -- one selector call at emission over a transient pairing, instead of a
  // wrapper layer per pull. Itself a citizen: further Selects compose onto the selector
  // (closure by signature).
  internal sealed class AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TProduct>
    : IAsyncSelectComposableTreenumerable<TProduct>
  {
    public AsyncRootfixScanProductTreenumerable(
      Func<IAsyncTreenumerator<TNode>> innerDepthFirstFactory,
      Func<IAsyncTreenumerator<TNode>> innerBreadthFirstFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed,
      Func<NodeAccumulation<TNode, TAccumulate>, TProduct> productSelector)
    {
      _InnerDepthFirstFactory = innerDepthFirstFactory;
      _InnerBreadthFirstFactory = innerBreadthFirstFactory;
      _Accumulator = accumulator;
      _Seed = seed;
      _ProductSelector = productSelector;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _InnerDepthFirstFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _InnerBreadthFirstFactory;
    private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;
    private readonly TAccumulate _Seed;
    private readonly Func<NodeAccumulation<TNode, TAccumulate>, TProduct> _ProductSelector;

    public IAsyncTreenumerator<TProduct> GetAsyncDepthFirstTreenumerator()
      => new AsyncRootfixScanProductDepthFirstTreenumerator<TNode, TAccumulate, TProduct>(
        _InnerDepthFirstFactory, _Accumulator, _Seed, _ProductSelector);

    public IAsyncTreenumerator<TProduct> GetAsyncBreadthFirstTreenumerator()
      => new AsyncRootfixScanProductBreadthFirstTreenumerator<TNode, TAccumulate, TProduct>(
        _InnerBreadthFirstFactory, _Accumulator, _Seed, _ProductSelector);

    public IAsyncSelectComposableTreenumerable<TResult> ComposeSelect<TResult>(Func<TProduct, TResult> selector)
    {
      var currentProductSelector = _ProductSelector;

      return new AsyncRootfixScanProductTreenumerable<TNode, TAccumulate, TResult>(
        _InnerDepthFirstFactory, _InnerBreadthFirstFactory, _Accumulator, _Seed,
        pairing => selector(currentProductSelector(pairing)));
    }
  }
}
