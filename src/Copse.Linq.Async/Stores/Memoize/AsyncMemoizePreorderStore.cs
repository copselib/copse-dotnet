using Copse.Async.Stores;
using Copse.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // Presents a memo's DFT dimension buffer as an IPreorderStore for the native playback
  // treenumerator (PreorderStoreDepthFirstTreenumerator). A struct so the playback's store calls
  // specialize and inline -- the same unboxed pattern as the engine's TChildEnumerator.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): SPI handle over AsyncMemoizePreorderBuffer (unboxing adapter, not a store of its own).
  internal readonly struct AsyncMemoizePreorderStore<TValue> : IAsyncPreorderStore<TValue>
  {
    public AsyncMemoizePreorderStore(AsyncMemoizePreorderBuffer<TValue> buffer)
    {
      _Buffer = buffer;
    }

    private readonly AsyncMemoizePreorderBuffer<TValue> _Buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> EnsureBufferedAsync(int index) => _Buffer.EnsureBufferedAsync(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> EnsureSubtreeClosedAsync(int index) => _Buffer.EnsureSubtreeClosedAsync(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSubtreeSize(int index) => _Buffer.GetSubtreeSize(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Buffer.GetValue(index);
  }
}
