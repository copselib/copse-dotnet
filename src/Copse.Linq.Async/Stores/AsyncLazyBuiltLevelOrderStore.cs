using Copse.Async.Stores;
using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // An IAsyncLevelOrderStore over a level-order capture that does not exist yet: the first grow
  // call awaits the one-shot build and every call after that answers from the completed
  // LevelOrderArrayStore. AsyncLazyBuiltPreorderStore's structural dual -- see that store for
  // why the deferral rides the grow seam rather than the treenumerator factory. The sync twin
  // (LazyBuiltLevelOrderStore) is generated from this.
  //
  // GetFirstChildIndex/GetValue are pure reads and stay synchronous: the decoder contract
  // guarantees a grow call precedes every read, so the store is always built by the time they
  // run. Single-threaded by contract, like every treenumerator in the library.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): level-order x growing x one-shot-build feed.
  internal sealed class AsyncLazyBuiltLevelOrderStore<TValue> : IAsyncLevelOrderStore<TValue>
  {
    public AsyncLazyBuiltLevelOrderStore(Func<ValueTask<AsyncLevelOrderArrayStore<TValue>>> build)
    {
      _Build = build;
    }

    private Func<ValueTask<AsyncLevelOrderArrayStore<TValue>>> _Build;
    private AsyncLevelOrderArrayStore<TValue> _Store;

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
    public ValueTask<bool> EnsureRootAvailableAsync(int k)
    {
      if (_Build != null)
        return BuildThenEnsureRootAvailableAsync(k);

      return _Store.EnsureRootAvailableAsync(k);
    }

    private async ValueTask<bool> BuildThenEnsureRootAvailableAsync(int k)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return await _Store.EnsureRootAvailableAsync(k).ConfigureAwait(false);
    }

    public ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k)
    {
      if (_Build != null)
        return BuildThenEnsureChildAvailableAsync(parentIndex, k);

      return _Store.EnsureChildAvailableAsync(parentIndex, k);
    }

    private async ValueTask<bool> BuildThenEnsureChildAvailableAsync(int parentIndex, int k)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return await _Store.EnsureChildAvailableAsync(parentIndex, k).ConfigureAwait(false);
    }

    public int GetFirstChildIndex(int parentIndex) => _Store.GetFirstChildIndex(parentIndex);

    public TValue GetValue(int index) => _Store.GetValue(index);
  }
}
