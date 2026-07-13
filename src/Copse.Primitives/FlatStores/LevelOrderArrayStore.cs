using System.Runtime.CompilerServices;

namespace Copse
{
  // A COMPLETED level-order store over plain arrays: values[i] in level order, node i's children
  // spanning [firstChildIndices[i], firstChildIndices[i] + childCounts[i]), the roots the
  // depth-0 prefix. The trivial implementation of ILevelOrderStore and PreorderArrayStore's
  // structural dual -- nothing grows, so the Ensure* hooks are pure reads. Wrapped in a
  // LevelOrderTreenumerable{TValue, LevelOrderArrayStore{TValue}} this is a full ITreenumerable
  // (random access affords both dimensions).
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): level-order x completed x no feed.
  public readonly struct LevelOrderArrayStore<TValue> : ILevelOrderStore<TValue>
  {
    public LevelOrderArrayStore(TValue[] values, int[] firstChildIndices, int[] childCounts, int rootCount)
    {
      _Values = values;
      _FirstChildIndices = firstChildIndices;
      _ChildCounts = childCounts;
      _RootCount = rootCount;
    }

    private readonly TValue[] _Values;
    private readonly int[] _FirstChildIndices;
    private readonly int[] _ChildCounts;
    private readonly int _RootCount;

    public int Count => _Values.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EnsureRootAvailable(int k) => k < _RootCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EnsureChildAvailable(int parentIndex, int k) => k < _ChildCounts[parentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Values[index];
  }
}
