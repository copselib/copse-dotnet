using Copse.Async.Stores;
using Copse.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // Presents a memo's BFT dimension buffer as an ILevelOrderStore for the native playback
  // treenumerator (LevelOrderStoreBreadthFirstTreenumerator). A struct so the playback's store
  // calls specialize and inline -- the same unboxed pattern as the engine's TChildEnumerator.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): SPI handle over AsyncMemoizeLevelOrderBuffer (unboxing adapter, not a store of its own).
  internal readonly struct AsyncMemoizeLevelOrderStore<TValue> : IAsyncLevelOrderStore<TValue>
  {
    public AsyncMemoizeLevelOrderStore(AsyncMemoizeLevelOrderBuffer<TValue> buffer)
    {
      _Buffer = buffer;
    }

    private readonly AsyncMemoizeLevelOrderBuffer<TValue> _Buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureRootAvailableAsync(int k) => _Buffer.EnsureRootAvailableAsync(k);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k) => _Buffer.EnsureChildAvailableAsync(parentIndex, k);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstChildIndex(int parentIndex) => _Buffer.GetFirstChildIndex(parentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Buffer.GetValue(index);
  }
}
