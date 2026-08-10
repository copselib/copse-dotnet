namespace Copse.Stores
{
  /// <summary>
  /// The single-node child pull over a level-order store: node index's children are a CONTIGUOUS
  /// run starting at <c>GetFirstChildIndex</c> -- the axis the encoding half-materializes, so the
  /// pull is an offset plus a bounds probe (contrast the preorder pull's subtree-size hops; the
  /// two stores are axis-cost duals, docs/WALKER_DESIGN.md).
  ///
  /// <para>Constructed with <see cref="VirtualRootParent"/>, it serves the virtual forest-root
  /// position instead (the roots pull): root ordinal k is buffer index k, the store's leading
  /// entries.</para>
  /// </summary>
  public struct LevelOrderStoreChildEnumerator<TValue, TStore> : IChildEnumerator<int>
    where TStore : ILevelOrderStore<TValue>
  {
    /// <summary>Parent sentinel meaning "the virtual forest-root position" (the roots pull).</summary>
    public const int VirtualRootParent = -1;

    public LevelOrderStoreChildEnumerator(TStore store, int parentIndex)
    {
      _Store = store;
      _ParentIndex = parentIndex;
      _FirstChildIndex = 0;
      _NextSiblingIndex = 0;
    }

    private readonly TStore _Store;
    private readonly int _ParentIndex;
    private int _FirstChildIndex;
    private int _NextSiblingIndex;

    public ChildResult<int> MoveNext()
    {
      var siblingIndex = _NextSiblingIndex;

      if (_ParentIndex == VirtualRootParent)
      {
        if (!_Store.EnsureRootAvailable(siblingIndex))
          return default;

        _NextSiblingIndex = siblingIndex + 1;

        return new ChildResult<int>(new NodeAndSiblingIndex<int>(siblingIndex, siblingIndex));
      }

      if (!_Store.EnsureChildAvailable(_ParentIndex, siblingIndex))
        return default;

      // GetFirstChildIndex is only meaningful once the parent has an available child, so it is
      // fetched behind the first successful availability probe.
      if (siblingIndex == 0)
        _FirstChildIndex = _Store.GetFirstChildIndex(_ParentIndex);

      _NextSiblingIndex = siblingIndex + 1;

      return new ChildResult<int>(new NodeAndSiblingIndex<int>(_FirstChildIndex + siblingIndex, siblingIndex));
    }

    public void Dispose()
    {
    }
  }
}
