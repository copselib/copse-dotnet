using System.Collections.Generic;
using Copse.Core;
using Copse.Stores;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  /// <summary>
  /// The walkable citizen of the flat family's level-order half:
  /// <see cref="WalkablePreorderTreenumerable{TValue, TStore}"/>'s structural dual, with the same
  /// ordinal handle and the same shape everywhere -- streaming delegates to the store
  /// treenumerators (conformance-pinned), child pulls ride the store's own adjacency, the parent
  /// axis rides a lazily built one-pass index.
  ///
  /// <para>The duality is in the axis costs: level order half-materializes the CHILD axis
  /// (contiguous runs behind <c>GetFirstChildIndex</c> -- the pull needs no arithmetic over
  /// subtree sizes), and its parent index build needs no stack, because child runs tile the
  /// buffer in parent order so the parent sequence is monotone -- a two-cursor merge, against
  /// the preorder build's open-span walk. What preorder makes contiguous (subtrees) level order
  /// scatters, and vice versa (levels, sibling runs). Not thread-safe (PoC).</para>
  /// </summary>
  public sealed class WalkableLevelOrderTreenumerable<TValue, TStore>
    : IWalkableTreenumerable<TValue, int, LevelOrderStoreChildEnumerator<TValue, TStore>>
    where TStore : ILevelOrderStore<TValue>
  {
    public WalkableLevelOrderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly TStore _Store;
    private int[] _ParentIndexes;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new LevelOrderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new LevelOrderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);

    public TValue GetValue(int node)
      => _Store.GetValue(node);

    public ParentResult<int> GetParent(int node)
    {
      if (_ParentIndexes == null)
        _ParentIndexes = BuildParentIndexes();

      var parentIndex = _ParentIndexes[node];

      return parentIndex == NoParent
        ? default
        : new ParentResult<int>(parentIndex);
    }

    public LevelOrderStoreChildEnumerator<TValue, TStore> GetChildEnumerator(int node)
      => new LevelOrderStoreChildEnumerator<TValue, TStore>(_Store, node);

    public LevelOrderStoreChildEnumerator<TValue, TStore> GetRootEnumerator()
      => new LevelOrderStoreChildEnumerator<TValue, TStore>(
        _Store,
        LevelOrderStoreChildEnumerator<TValue, TStore>.VirtualRootParent);

    // One pass in level order, no stack: the roots seed the index, then the parent cursor sweeps
    // every indexed node in order, writing itself under each of its children. The parent cursor
    // can never overtake the growth it causes until the tree is exhausted, and the store's own
    // GetFirstChildIndex stays the authority on where each child run lands.
    private int[] BuildParentIndexes()
    {
      var parentIndexes = new List<int>();

      var rootOrdinal = 0;
      while (_Store.EnsureRootAvailable(rootOrdinal))
      {
        parentIndexes.Add(NoParent);
        rootOrdinal++;
      }

      for (var parentIndex = 0; parentIndex < parentIndexes.Count; parentIndex++)
      {
        var childOrdinal = 0;
        var firstChildIndex = 0;

        while (_Store.EnsureChildAvailable(parentIndex, childOrdinal))
        {
          if (childOrdinal == 0)
            firstChildIndex = _Store.GetFirstChildIndex(parentIndex);

          var childIndex = firstChildIndex + childOrdinal;

          while (parentIndexes.Count <= childIndex)
            parentIndexes.Add(NoParent);

          parentIndexes[childIndex] = parentIndex;
          childOrdinal++;
        }
      }

      return parentIndexes.ToArray();
    }
  }
}
