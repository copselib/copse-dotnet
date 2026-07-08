using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerators
{
  // An IAsyncPreorderStore over a preorder capture that does not exist yet: the first grow call
  // awaits the one-shot build (an awaited walk of an async source into flat preorder arrays)
  // and every call after that answers from the completed PreorderArrayStore.
  //
  // This is the async transcription of the Lazy&lt;PreorderTreenumerable&gt; shape the sync
  // LeaffixScan/Invert use: a sync-signature treenumerator factory cannot await, so the deferral
  // moves from the treenumerable (Lazy.Value inside the factory) to the store's grow seam --
  // which the async store decoders already await. There is no sync twin; this type exists only
  // because async cannot defer inside a sync-signature factory.
  //
  // GetValue/GetSubtreeSize are pure reads and stay synchronous: the decoder contract guarantees
  // a grow call precedes every read, so the store is always built by the time they run.
  // Single-threaded by contract, like every treenumerator in the library.
  internal sealed class AsyncLazyBuiltPreorderStore<TValue> : IAsyncPreorderStore<TValue>
  {
    public AsyncLazyBuiltPreorderStore(Func<ValueTask<PreorderArrayStore<TValue>>> build)
    {
      _Build = build;
    }

    private Func<ValueTask<PreorderArrayStore<TValue>>> _Build;
    private PreorderArrayStore<TValue> _Store;

    private async ValueTask EnsureBuiltAsync()
    {
      if (_Build == null)
        return;

      _Store = await _Build().ConfigureAwait(false);
      _Build = null; // the build runs once; drop the closure (and whatever source it captured)
    }

    public async ValueTask<bool> EnsureBufferedAsync(int index)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return _Store.EnsureBuffered(index);
    }

    public async ValueTask<int> EnsureSubtreeClosedAsync(int index)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return _Store.EnsureSubtreeClosed(index);
    }

    public int GetSubtreeSize(int index) => _Store.GetSubtreeSize(index);

    public TValue GetValue(int index) => _Store.GetValue(index);
  }
}
