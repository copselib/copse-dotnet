namespace Copse.Linq.Treenumerators
{
  // Presents a memo's DFT dimension buffer as an IPreorderStore for the native playback
  // treenumerator (PreorderStoreDepthFirstTreenumerator). A struct so the playback's store calls
  // specialize and inline -- the same unboxed pattern as the engine's TChildEnumerator.
  internal readonly struct MemoizeDepthFirstStore<TValue> : IPreorderStore<TValue>
  {
    public MemoizeDepthFirstStore(MemoizeDepthFirstBuffer<TValue> buffer)
    {
      _Buffer = buffer;
    }

    private readonly MemoizeDepthFirstBuffer<TValue> _Buffer;

    public bool EnsureBuffered(int index) => _Buffer.EnsureBuffered(index);

    public int EnsureSubtreeClosed(int index) => _Buffer.EnsureSubtreeClosed(index);

    public int GetSubtreeSize(int index) => _Buffer.GetSubtreeSize(index);

    public TValue GetValue(int index) => _Buffer.GetValue(index);
  }
}
