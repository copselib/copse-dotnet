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
  /// treenumerators (conformance-pinned), the parent axis rides a lazily built one-pass index.
  ///
  /// <para>The duality is in the axis costs, and the indexed child contract makes it vivid: the
  /// level-order encoding IS the VisualTreeHelper shape already -- <c>GetChildAt</c> is a bounds
  /// probe plus an offset (<c>GetFirstChildIndex + childIndex</c>, contiguous runs), no index
  /// build needed -- while its parent index build is a STACKLESS two-cursor merge (child runs
  /// tile the buffer in parent order, so the parent sequence is monotone) against the preorder
  /// build's open-span walk. What preorder makes contiguous (subtrees) level order scatters, and
  /// vice versa (levels, sibling runs). Not thread-safe (PoC).</para>
  /// </summary>
  public sealed class WalkableLevelOrderTreenumerable<TValue, TStore>
    : IWalkableTreenumerable<TValue, int>
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

    public ChildResult<int> GetChildAt(int node, int childIndex)
    {
      if (childIndex < 0 || !_Store.EnsureChildAvailable(node, childIndex))
        return default;

      // GetFirstChildIndex is meaningful once the parent has an available child, which the
      // successful probe above just established.
      return new ChildResult<int>(new NodeAndSiblingIndex<int>(_Store.GetFirstChildIndex(node) + childIndex, childIndex));
    }

    public ChildResult<int> GetRootAt(int rootIndex)
    {
      if (rootIndex < 0 || !_Store.EnsureRootAvailable(rootIndex))
        return default;

      // Root ordinal k is buffer index k: the roots are the store's leading entries.
      return new ChildResult<int>(new NodeAndSiblingIndex<int>(rootIndex, rootIndex));
    }

    public int GetChildCount(int node)
    {
      // The store protocol speaks availability probes, not counts, so the count is the probe
      // walked to its first miss -- each probe an O(1) read on a completed store.
      var childCount = 0;
      while (_Store.EnsureChildAvailable(node, childCount))
        childCount++;

      return childCount;
    }

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
