using Copse.Core;
using Copse.Stores;
using Copse.Treenumerators;

namespace Copse.Treenumerables
{
  /// <summary>
  /// The walkable citizen of the flat family (PoC): any <see cref="IPreorderStore{TValue}"/>
  /// becomes an <see cref="IWalkableTreenumerable{TValue, TNode, TChildEnumerator}"/> with the
  /// ordinal (preorder index) as the node handle -- handle equality is index equality, so the
  /// library's no-node-equality pledge holds with nothing asked of <typeparamref name="TValue"/>.
  ///
  /// <para>Streaming delegates to the same store treenumerators as
  /// <see cref="PreorderTreenumerable{TValue, TStore}"/>, so the walkable's visit stream IS the
  /// flat family's (conformance-pinned). Child pulls are span arithmetic. The parent axis rides a
  /// parent index built lazily on the first upward step -- one O(n) pass; the recording-to-index
  /// upgrade of docs/WALKER_DESIGN.md -- which, being an index over the whole store, forces a
  /// still-growing store to complete first. Not thread-safe (PoC).</para>
  /// </summary>
  public sealed class WalkablePreorderTreenumerable<TValue, TStore>
    : IWalkableTreenumerable<TValue, int, PreorderStoreChildEnumerator<TValue, TStore>>
    where TStore : IPreorderStore<TValue>
  {
    public WalkablePreorderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private const int NoParent = -1;

    private readonly TStore _Store;
    private int[] _ParentIndexes;

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new PreorderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new PreorderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);

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

    public PreorderStoreChildEnumerator<TValue, TStore> GetChildEnumerator(int node)
    {
      var subtreeSize = _Store.EnsureSubtreeClosed(node);

      return new PreorderStoreChildEnumerator<TValue, TStore>(_Store, node + 1, node + subtreeSize);
    }

    public PreorderStoreChildEnumerator<TValue, TStore> GetRootEnumerator()
      => new PreorderStoreChildEnumerator<TValue, TStore>(
        _Store,
        0,
        PreorderStoreChildEnumerator<TValue, TStore>.Unbounded);

    // One pass over the preorder sequence with a stack of open subtree spans: a node's parent is
    // the nearest enclosing span still open when the node is reached.
    private int[] BuildParentIndexes()
    {
      var nodeCount = 0;
      while (_Store.EnsureBuffered(nodeCount))
        nodeCount++;

      var parentIndexes = new int[nodeCount];
      var openNodeIndexes = new int[nodeCount];
      var openSpanEnds = new int[nodeCount];
      var openSpanCount = 0;

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        while (openSpanCount > 0 && nodeIndex >= openSpanEnds[openSpanCount - 1])
          openSpanCount--;

        parentIndexes[nodeIndex] = openSpanCount > 0
          ? openNodeIndexes[openSpanCount - 1]
          : NoParent;

        openNodeIndexes[openSpanCount] = nodeIndex;
        openSpanEnds[openSpanCount] = nodeIndex + _Store.EnsureSubtreeClosed(nodeIndex);
        openSpanCount++;
      }

      return parentIndexes;
    }
  }
}
