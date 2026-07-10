using System.Runtime.CompilerServices;

namespace Copse
{
  // A COMPLETED preorder store over plain arrays: values[i] in preorder, node i's subtree
  // spanning [i, i + subtreeSizes[i]). The trivial implementation of IPreorderStore -- nothing
  // grows, so the Ensure* hooks are pure reads. Wrapped in a
  // PreorderTreenumerable{TValue, PreorderArrayStore{TValue}} this is a full ITreenumerable
  // (random access affords both dimensions); it is the store shape PreorderTree dissolves into.
  public readonly struct PreorderArrayStore<TValue> : IPreorderStore<TValue>
  {
    public PreorderArrayStore(TValue[] values, int[] subtreeSizes)
    {
      _Values = values;
      _SubtreeSizes = subtreeSizes;
    }

    private readonly TValue[] _Values;
    private readonly int[] _SubtreeSizes;

    public int Count => _Values.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EnsureBuffered(int index) => index < _Values.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EnsureSubtreeClosed(int index) => _SubtreeSizes[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Values[index];
  }
}
