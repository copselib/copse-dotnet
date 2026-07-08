using Copse.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerators
{
  // Presents a memo's DFT dimension buffer as an IPreorderStore for the native playback
  // treenumerator (PreorderStoreDepthFirstTreenumerator). A struct so the playback's store calls
  // specialize and inline -- the same unboxed pattern as the engine's TChildEnumerator.
  internal readonly struct AsyncMemoizeDepthFirstStore<TValue> : IAsyncPreorderStore<TValue>
  {
    public AsyncMemoizeDepthFirstStore(AsyncMemoizeDepthFirstBuffer<TValue> buffer)
    {
      _Buffer = buffer;
    }

    private readonly AsyncMemoizeDepthFirstBuffer<TValue> _Buffer;

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
