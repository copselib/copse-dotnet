using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The scan family's citizen buffer (SELECT_INTO_CAPTURES_DESIGN.md): a deferred capture
  // whose product is productSelector(node, accumulate) over a SHARED fold pass -- the
  // canonical pairing by default, and ComposeSelect returns a SIBLING variant over the same
  // pass with the selector composed (f after the current selector: the pair still forms per
  // node, on the stack, never stored). Closure is the signature's doing; the at-most-once
  // rule is the pass's. The inner buffer is constructed eagerly -- construction is cheap,
  // the O(n) zip is pinned to the first pull through the lazy store, and probes ride the
  // product store at birth (the InPlaceLeaffixScan wiring, hoisted).
  internal sealed class AsyncScanProductBuffer<TSource, TAccumulate, TProduct>
    : IAsyncSelectComposableTreenumerableBuffer<TProduct>
  {
    public AsyncScanProductBuffer(
      AsyncScanFoldPass<TSource, TAccumulate> foldPass,
      Func<TSource, TAccumulate, TProduct> productSelector,
      bool isCanonicalPairing = false)
    {
      _FoldPass = foldPass;
      _ProductSelector = productSelector;

      var productStore = new AsyncLazyPreorderStore<TProduct>(() => ZipAsync(foldPass, productSelector, isCanonicalPairing));

      _Inner = new AsyncTreenumerableBuffer<TProduct>(
        new AsyncPreorderTreenumerable<TProduct, AsyncLazyPreorderStore<TProduct>>(productStore),
        BufferLayout.Preorder,
        new AsyncPreorderAdjacencyIndex<TProduct, AsyncLazyPreorderStore<TProduct>>(productStore));
    }

    private readonly AsyncScanFoldPass<TSource, TAccumulate> _FoldPass;
    private readonly Func<TSource, TAccumulate, TProduct> _ProductSelector;
    private readonly AsyncTreenumerableBuffer<TProduct> _Inner;

    public BufferLayout? NativeLayout => _Inner.NativeLayout;

    public IAsyncTreenumerator<TProduct> GetAsyncDepthFirstTreenumerator()
      => _Inner.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TProduct> GetAsyncBreadthFirstTreenumerator()
      => _Inner.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<AsyncTreeWalkerResult<TProduct, int>> TryGetTreeWalkerAsync()
      => _Inner.TryGetTreeWalkerAsync();

    public IAsyncSelectComposableTreenumerableBuffer<TResult> ComposeSelect<TResult>(Func<TProduct, TResult> selector)
    {
      var currentProductSelector = _ProductSelector;

      return new AsyncScanProductBuffer<TSource, TAccumulate, TResult>(
        _FoldPass,
        (node, accumulate) => selector(currentProductSelector(node, accumulate)));
    }

    // The finisher: one zip over the shared pass into this variant's own product store,
    // the skeleton array shared. The lazy store releases this closure after the build, so
    // a built variant no longer references the pass. The canonical variant asks the pass to
    // FUSE its pairs into the fold loop (the first-caller fusion); when it built first, the
    // fused array IS its product -- the cast is exact, TProduct is the pairing when the
    // flag is set -- and no zip runs at all.
    private static async ValueTask<AsyncPreorderArrayStore<TProduct>> ZipAsync(
      AsyncScanFoldPass<TSource, TAccumulate> foldPass,
      Func<TSource, TAccumulate, TProduct> productSelector,
      bool isCanonicalPairing)
    {
      var (artifacts, fusedPairProducts) = await foldPass.EnsureAsync(isCanonicalPairing).ConfigureAwait(false);

      if (isCanonicalPairing && fusedPairProducts != null)
        return new AsyncPreorderArrayStore<TProduct>((TProduct[])(object)fusedPairProducts, artifacts.SubtreeSizes);

      var products = new TProduct[artifacts.Count];

      for (var nodeIndex = 0; nodeIndex < products.Length; nodeIndex++)
        products[nodeIndex] = productSelector(artifacts.ValueAt(nodeIndex), artifacts.Accumulates[nodeIndex]);

      return new AsyncPreorderArrayStore<TProduct>(products, artifacts.SubtreeSizes);
    }
  }
}
