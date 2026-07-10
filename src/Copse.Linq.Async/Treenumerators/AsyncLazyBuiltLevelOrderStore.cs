using Copse.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerators
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
  internal sealed class AsyncLazyBuiltLevelOrderStore<TValue> : IAsyncLevelOrderStore<TValue>
  {
    public AsyncLazyBuiltLevelOrderStore(Func<ValueTask<LevelOrderArrayStore<TValue>>> build)
    {
      _Build = build;
    }

    private Func<ValueTask<LevelOrderArrayStore<TValue>>> _Build;
    private LevelOrderArrayStore<TValue> _Store;

    private async ValueTask EnsureBuiltAsync()
    {
      if (_Build == null)
        return;

      _Store = await _Build().ConfigureAwait(false);
      _Build = null; // the build runs once; drop the closure (and whatever source it captured)
    }

    public async ValueTask<bool> EnsureRootAvailableAsync(int k)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return _Store.EnsureRootAvailable(k);
    }

    public async ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k)
    {
      await EnsureBuiltAsync().ConfigureAwait(false);
      return _Store.EnsureChildAvailable(parentIndex, k);
    }

    public int GetFirstChildIndex(int parentIndex) => _Store.GetFirstChildIndex(parentIndex);

    public TValue GetValue(int index) => _Store.GetValue(index);
  }
}
