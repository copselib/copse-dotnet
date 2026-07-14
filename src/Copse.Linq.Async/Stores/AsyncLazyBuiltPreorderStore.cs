using Copse.Async.Stores;
using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // An IAsyncPreorderStore over a preorder capture that does not exist yet: the first grow call
  // awaits the one-shot build (an awaited walk of an async source into flat preorder arrays)
  // and every call after that answers from the completed PreorderArrayStore.
  //
  // This replaced the Lazy&lt;PreorderTreenumerable&gt; shape the hand-written sync
  // LeaffixScan/Invert used: a sync-signature treenumerator factory cannot await, so the deferral
  // moves from the treenumerable (Lazy.Value inside the factory) to the store's grow seam --
  // which the store decoders already own. The sync twin (LazyBuiltPreorderStore) is generated
  // from this and is what the generated sync LeaffixScan/Invert defer through.
  //
  // GetValue/GetSubtreeSize are pure reads and stay synchronous: the decoder contract guarantees
  // a grow call precedes every read, so the store is always built by the time they run.
  // Single-threaded by contract, like every treenumerator in the library.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): preorder x growing x one-shot-build feed.
  internal sealed class AsyncLazyBuiltPreorderStore<TValue> : IAsyncPreorderStore<TValue>
  {
    public AsyncLazyBuiltPreorderStore(Func<ValueTask<AsyncPreorderArrayStore<TValue>>> build)
    {
      _Build = build;
    }

    private Func<ValueTask<AsyncPreorderArrayStore<TValue>>> _Build;
    private AsyncPreorderArrayStore<TValue> _Store;

    private async ValueTask EnsureBuiltAsync()
    {
      if (_Build == null)
        return;

      _Store = await _Build().ConfigureAwait(false);
      _Build = null; // the build runs once; drop the closure (and whatever source it captured)
    }

    // The grow calls split along the built/unbuilt line: once built (every call after the
    // first), the answer is a plain read with no state machine; only the one-shot build path is
    // async. The callers' probes see a completed ValueTask and stay on their own fast paths.
    public ValueTask<bool> EnsureBufferedAsync(int index)
    {
      if (_Build != null)
        return BuildThenEnsureBufferedAsync(index);

      return _Store.EnsureBufferedAsync(index);
    }

    private async ValueTask<bool> BuildThenEnsureBufferedAsync(int index)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return await _Store.EnsureBufferedAsync(index).ConfigureAwait(false);
    }

    public ValueTask<int> EnsureSubtreeClosedAsync(int index)
    {
      if (_Build != null)
        return BuildThenEnsureSubtreeClosedAsync(index);

      return _Store.EnsureSubtreeClosedAsync(index);
    }

    private async ValueTask<int> BuildThenEnsureSubtreeClosedAsync(int index)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return await _Store.EnsureSubtreeClosedAsync(index).ConfigureAwait(false);
    }

    public int GetSubtreeSize(int index) => _Store.GetSubtreeSize(index);

    public TValue GetValue(int index) => _Store.GetValue(index);
  }
}
