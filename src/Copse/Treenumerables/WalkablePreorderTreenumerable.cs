using Copse.Core;
using Copse.Stores;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  /// <summary>
  /// The walkable citizen of the flat family (PoC): any <see cref="IPreorderStore{TValue}"/>
  /// becomes an <see cref="IWalkableTreenumerable{TValue, THandle}"/> with the ordinal (preorder
  /// index) as the handle -- handle equality is index equality, so the library's
  /// no-node-equality pledge holds with nothing asked of <typeparamref name="TValue"/>.
  ///
  /// <para>Streaming delegates to the same store treenumerators as
  /// <see cref="PreorderTreenumerable{TValue, TStore}"/>, so the walkable's visit stream IS the
  /// flat family's (conformance-pinned). The whole adjacency surface rides ONE lazily built
  /// index -- parents, a CSR child index (~2n ints; the honest-O(1)-indexer precedent -- child
  /// k by subtree-span hopping would be O(k) per probe and quadratic per family), and the root
  /// list -- built in one O(n) pass on the first adjacency call; the recording-to-index upgrade
  /// of docs/WALKER_DESIGN.md, which, being an index over the whole store, forces a
  /// still-growing store to complete first. Not thread-safe (PoC).</para>
  /// </summary>
  internal sealed class WalkablePreorderTreenumerable<TValue, TStore>
    : IWalkableTreenumerable<TValue, int>
    where TStore : IPreorderStore<TValue>
  {
    public WalkablePreorderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly TStore _Store;

    private int[] _ParentIndexes;
    private int[] _ChildIndexStarts;   // length n + 1; node i's children at [_ChildIndexStarts[i], _ChildIndexStarts[i + 1])
    private int[] _ChildIndexes;       // the CSR payload, children in sibling order
    private int[] _RootIndexes;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new PreorderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new PreorderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);

    public TValue GetValue(int handle)
    {
      // The store contract: a grow call precedes every read (a deferred or still-growing store
      // may not have built yet -- ordinal handles are guessable ints, so the deref cannot assume
      // the probe machinery ran first).
      _Store.EnsureBuffered(handle);

      return _Store.GetValue(handle);
    }

    public ParentResult<int> GetParent(int handle)
    {
      EnsureAdjacencyIndexes();

      var parentIndex = _ParentIndexes[handle];

      return parentIndex == NoParent
        ? default
        : new ParentResult<int>(parentIndex);
    }

    public ChildResult<int> GetChildAt(int handle, int childIndex)
    {
      EnsureAdjacencyIndexes();

      var childStart = _ChildIndexStarts[handle];
      var childCount = _ChildIndexStarts[handle + 1] - childStart;

      if (childIndex < 0 || childIndex >= childCount)
        return default;

      return new ChildResult<int>(new NodeAndSiblingIndex<int>(_ChildIndexes[childStart + childIndex], childIndex));
    }

    public ChildResult<int> GetRootAt(int rootIndex)
    {
      EnsureAdjacencyIndexes();

      if (rootIndex < 0 || rootIndex >= _RootIndexes.Length)
        return default;

      return new ChildResult<int>(new NodeAndSiblingIndex<int>(_RootIndexes[rootIndex], rootIndex));
    }

    public int GetChildCount(int handle)
    {
      EnsureAdjacencyIndexes();

      return _ChildIndexStarts[handle + 1] - _ChildIndexStarts[handle];
    }

    private void EnsureAdjacencyIndexes()
    {
      if (_ParentIndexes == null)
        BuildAdjacencyIndexes();
    }

    // One pass over the preorder sequence with a stack of open subtree spans computes the
    // parents (a node's parent is the nearest enclosing span still open when the node is
    // reached); the CSR child index and root list then fill by a second ascending scan --
    // children of a node appear in preorder in sibling order, so appending in scan order IS
    // sibling order.
    private void BuildAdjacencyIndexes()
    {
      var nodeCount = 0;
      while (_Store.EnsureBuffered(nodeCount))
        nodeCount++;

      var parentIndexes = new int[nodeCount];
      var openNodeIndexes = new int[nodeCount];
      var openSpanEnds = new int[nodeCount];
      var openSpanCount = 0;
      var rootCount = 0;

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        while (openSpanCount > 0 && nodeIndex >= openSpanEnds[openSpanCount - 1])
          openSpanCount--;

        if (openSpanCount > 0)
        {
          parentIndexes[nodeIndex] = openNodeIndexes[openSpanCount - 1];
        }
        else
        {
          parentIndexes[nodeIndex] = NoParent;
          rootCount++;
        }

        openNodeIndexes[openSpanCount] = nodeIndex;
        openSpanEnds[openSpanCount] = nodeIndex + _Store.EnsureSubtreeClosed(nodeIndex);
        openSpanCount++;
      }

      var childIndexStarts = new int[nodeCount + 1];

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        if (parentIndexes[nodeIndex] != NoParent)
          childIndexStarts[parentIndexes[nodeIndex] + 1]++;
      }

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        childIndexStarts[nodeIndex + 1] += childIndexStarts[nodeIndex];

      var childIndexes = new int[nodeCount - rootCount];
      var rootIndexes = new int[rootCount];
      var childFillCursors = new int[nodeCount];
      var rootFillCursor = 0;

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        var parentIndex = parentIndexes[nodeIndex];

        if (parentIndex == NoParent)
        {
          rootIndexes[rootFillCursor] = nodeIndex;
          rootFillCursor++;
        }
        else
        {
          childIndexes[childIndexStarts[parentIndex] + childFillCursors[parentIndex]] = nodeIndex;
          childFillCursors[parentIndex]++;
        }
      }

      _ChildIndexStarts = childIndexStarts;
      _ChildIndexes = childIndexes;
      _RootIndexes = rootIndexes;
      _ParentIndexes = parentIndexes;   // last: its null-ness is the built flag
    }
  }
}
