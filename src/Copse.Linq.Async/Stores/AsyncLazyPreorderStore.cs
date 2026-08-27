using Copse.Stores;
using Copse;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Stores
{
  // An IAsyncPreorderStore over a preorder capture that does not exist yet: the first grow call
  // awaits the one-shot build (an awaited walk of an async source into flat preorder arrays)
  // and every call after that answers from the completed PreorderArrayStore.
  //
  // This replaced the Lazy&lt;PreorderTreenumerable&gt; shape the hand-written sync
  // LeaffixDispatch/Invert used: a sync-signature treenumerator factory cannot await, so the deferral
  // moves from the treenumerable (Lazy.Value inside the factory) to the store's grow seam --
  // which the store decoders already own. The sync twin (LazyPreorderStore) is generated
  // from this and is what the generated sync LeaffixDispatch/Invert defer through.
  //
  // GetNode/GetSubtreeSize are pure reads and stay synchronous: the decoder contract guarantees
  // a grow call precedes every read, so the store is always built by the time they run.
  // Single-threaded by contract, like every treenumerator in the library.
  //
  // Taxonomy (design-docs/STORE_FAMILY_REVIEW.md): preorder x growing x one-shot-build feed.
  internal sealed class AsyncLazyPreorderStore<TNode> : IAsyncPreorderStore<TNode>
  {
    public AsyncLazyPreorderStore(Func<ValueTask<AsyncPreorderArrayStore<TNode>>> build)
    {
      _Build = build;
    }

    private Func<ValueTask<AsyncPreorderArrayStore<TNode>>> _Build;
    private AsyncPreorderArrayStore<TNode> _Store;

    // The bulk-fold seam's forcing door (the bulk-fold seam, absorbed into LeaffixScan): hand the built array
    // store over -- whole-tree algorithms read raw arithmetic, not per-probe dispatch.
    internal async ValueTask<AsyncPreorderArrayStore<TNode>> EnsureBuiltStoreAsync()
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return _Store;
    }

    // The reclaim seam: non-forcing build-state facts, so the buffer can
    // re-seat a birth-bound index over the built array store once the one-shot build ran.
    internal bool IsBuilt => _Build == null;

    internal AsyncPreorderArrayStore<TNode> BuiltStore => _Store;

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

    public TNode GetNode(int index) => _Store.GetNode(index);
  }
}
