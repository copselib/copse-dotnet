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

    // The finisher: the first-building variant's product fuses into the fold loop -- the
    // canonical pairing inline (the fused array IS the product; the cast is exact when the
    // flag is set), a composed product through the erased writer. Only a variant arriving
    // AFTER the build zips from the artifacts (direct array reads when the pass owns its
    // values; the reader delegate only for in-place passes, which never copy the store).
    // The lazy store releases this closure after the build, dropping the pass reference.
    private static async ValueTask<AsyncPreorderArrayStore<TProduct>> ZipAsync(
      AsyncScanFoldPass<TSource, TAccumulate> foldPass,
      Func<TSource, TAccumulate, TProduct> productSelector,
      bool isCanonicalPairing)
    {
      var writer = isCanonicalPairing ? null : new ArrayProductWriter(productSelector);
      var (artifacts, fusedPairProducts, writerRan) = await foldPass
        .EnsureAsync(new ScanBuildRequest<TSource, TAccumulate>(isCanonicalPairing, writer))
        .ConfigureAwait(false);

      if (isCanonicalPairing && fusedPairProducts != null)
        return new AsyncPreorderArrayStore<TProduct>((TProduct[])(object)fusedPairProducts, artifacts.SubtreeSizes);

      if (writerRan)
        return new AsyncPreorderArrayStore<TProduct>(writer.Products, artifacts.SubtreeSizes);

      var products = new TProduct[artifacts.Count];

      if (artifacts.Values != null)
      {
        var values = artifacts.Values;
        var accumulates = artifacts.Accumulates;

        for (var nodeIndex = 0; nodeIndex < products.Length; nodeIndex++)
          products[nodeIndex] = productSelector(values[nodeIndex], accumulates[nodeIndex]);
      }
      else
      {
        for (var nodeIndex = 0; nodeIndex < products.Length; nodeIndex++)
          products[nodeIndex] = productSelector(artifacts.ValueAt(nodeIndex), artifacts.Accumulates[nodeIndex]);
      }

      return new AsyncPreorderArrayStore<TProduct>(products, artifacts.SubtreeSizes);
    }

    private sealed class ArrayProductWriter : ScanProductWriter<TSource, TAccumulate>
    {
      public ArrayProductWriter(Func<TSource, TAccumulate, TProduct> productSelector)
      {
        _ProductSelector = productSelector;
      }

      private readonly Func<TSource, TAccumulate, TProduct> _ProductSelector;

      public TProduct[] Products;

      public override void Initialize(int nodeCount)
      {
        Products = new TProduct[nodeCount];
        Filled = true;
      }

      public override void Write(int nodeIndex, TSource value, TAccumulate accumulate)
        => Products[nodeIndex] = _ProductSelector(value, accumulate);
    }
  }
}
