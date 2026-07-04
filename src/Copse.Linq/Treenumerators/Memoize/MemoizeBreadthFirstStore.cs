namespace Copse.Linq.Treenumerators
{
  // Presents a memo's BFT dimension buffer as an ILevelOrderStore for the native playback
  // treenumerator (LevelOrderStoreBreadthFirstTreenumerator). A struct so the playback's store
  // calls specialize and inline -- the same unboxed pattern as the engine's TChildEnumerator.
  internal readonly struct MemoizeBreadthFirstStore<TValue> : ILevelOrderStore<TValue>
  {
    public MemoizeBreadthFirstStore(MemoizeBreadthFirstBuffer<TValue> buffer)
    {
      _Buffer = buffer;
    }

    private readonly MemoizeBreadthFirstBuffer<TValue> _Buffer;

    public bool EnsureRootAvailable(int k) => _Buffer.EnsureRootAvailable(k);

    public bool EnsureChildAvailable(int parentIndex, int k) => _Buffer.EnsureChildAvailable(parentIndex, k);

    public int GetFirstChildIndex(int parentIndex) => _Buffer.GetFirstChildIndex(parentIndex);

    public TValue GetValue(int index) => _Buffer.GetValue(index);
  }
}
