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
  // THE THIN SHAPE (the buffer-compose simplification, 2026-08-17 --
  // SELECT_INTO_CAPTURES_DESIGN.md): a buffer projection is the source buffer plus a
  // selector, and its deferred build is ONE counted array map off the source's completed
  // store. The source buffer is the sharing substrate -- replayable by contract, so
  // at-most-once is trivial -- and chained Selects compose the SELECTOR (closure by the
  // citizenship signature) while the build stays a single map: N projections, one pass,
  // one narrow store.
  //
  // This replaces the shared-fold-pass machinery (pass/variants/writer), which the
  // three-arm harness measured SLOWER than the transient pair store it existed to avoid
  // (~12ms/M-nodes of plumbing vs a ~2ms map that the narrow store's cheaper decode pays
  // back entirely -- projection over a buffer is effectively FREE in this shape). It also
  // returns scan results to plain concrete buffers, restoring the span fast path for
  // scan-of-scan (the Twice_Dft_Chain witness).
  internal sealed class AsyncProjectedTreenumerableBuffer<TSource, TResult>
    : IAsyncSelectComposableTreenumerableBuffer<TResult>
  {
    public AsyncProjectedTreenumerableBuffer(
      IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
    {
      _Source = source;
      _Selector = selector;

      var projectedStore = new AsyncLazyPreorderStore<TResult>(() => BuildAsync(source, selector));

      _Inner = new AsyncTreenumerableBuffer<TResult>(
        new AsyncPreorderTreenumerable<TResult, AsyncLazyPreorderStore<TResult>>(projectedStore),
        BufferLayout.Preorder,
        new AsyncPreorderAdjacencyIndex<TResult, AsyncLazyPreorderStore<TResult>>(projectedStore));
    }

    private readonly IAsyncTreenumerableBuffer<TSource> _Source;
    private readonly Func<TSource, TResult> _Selector;
    private readonly AsyncTreenumerableBuffer<TResult> _Inner;

    public BufferLayout? NativeLayout => _Inner.NativeLayout;

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator()
      => _Inner.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator()
      => _Inner.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<AsyncTreeWalkerResult<TResult, int>> TryGetTreeWalkerAsync()
      => _Inner.TryGetTreeWalkerAsync();

    public IAsyncSelectComposableTreenumerableBuffer<TNext> ComposeSelect<TNext>(Func<TResult, TNext> selector)
    {
      var currentSelector = _Selector;

      return new AsyncProjectedTreenumerableBuffer<TSource, TNext>(_Source, node => selector(currentSelector(node)));
    }

    // The one map. A concrete preorder source hands its raw store (values and skeleton read
    // by index, the skeleton copied alongside in the same loop); any other capture takes the
    // veneer walk -- a Select wrapper over the source's replay, captured once. Both routes
    // read the source's OWN storage, so the source is never re-enumerated at its origin.
    private static async ValueTask<AsyncPreorderArrayStore<TResult>> BuildAsync(
      IAsyncTreenumerableBuffer<TSource> source,
      Func<TSource, TResult> selector)
    {
      if (source is AsyncTreenumerableBuffer<TSource> concreteBuffer)
      {
        var (hasStore, sourceStore) = await concreteBuffer.TryGetPreorderStoreAsync().ConfigureAwait(false);

        if (hasStore)
        {
          var count = sourceStore.Count;
          var values = new TResult[count];
          var subtreeSizes = new int[count];

          for (var nodeIndex = 0; nodeIndex < count; nodeIndex++)
          {
            values[nodeIndex] = selector(sourceStore.GetValue(nodeIndex));
            subtreeSizes[nodeIndex] = sourceStore.GetSubtreeSize(nodeIndex);
          }

          return new AsyncPreorderArrayStore<TResult>(values, subtreeSizes);
        }
      }

      return await AsyncPreorderCapture
        .CaptureFromAsync(new AsyncSelectTreenumerable<TSource, TResult>(source, nodeContext => selector(nodeContext.Node)))
        .ConfigureAwait(false);
    }
  }
}
