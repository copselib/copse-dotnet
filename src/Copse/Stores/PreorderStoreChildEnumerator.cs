namespace Copse.Stores
{
  /// <summary>
  /// The single-node child pull over a preorder store: node index's children are the roots of the
  /// subtrees tiling <c>(index, index + subtreeSize)</c> -- each child is the first index after the
  /// previous child's subtree closes. Span arithmetic only; the cursor is the whole state.
  ///
  /// <para>Constructed with <see cref="Unbounded"/> as the exclusive end, it serves the virtual
  /// forest-root position instead (the roots pull): successive root subtrees until the store's
  /// stream exhausts.</para>
  /// </summary>
  public struct PreorderStoreChildEnumerator<TValue, TStore> : IChildEnumerator<int>
    where TStore : IPreorderStore<TValue>
  {
    /// <summary>Exclusive-end sentinel meaning "until the store exhausts" (the roots pull).</summary>
    public const int Unbounded = -1;

    public PreorderStoreChildEnumerator(TStore store, int firstChildIndex, int endExclusive)
    {
      _Store = store;
      _NextChildIndex = firstChildIndex;
      _EndExclusive = endExclusive;
      _NextSiblingIndex = 0;
    }

    private readonly TStore _Store;
    private readonly int _EndExclusive;
    private int _NextChildIndex;
    private int _NextSiblingIndex;

    public ChildResult<int> MoveNext()
    {
      var childIndex = _NextChildIndex;

      var hasChild = _EndExclusive == Unbounded
        ? _Store.EnsureBuffered(childIndex)
        : childIndex < _EndExclusive;

      if (!hasChild)
        return default;

      _NextChildIndex = childIndex + _Store.EnsureSubtreeClosed(childIndex);

      var siblingIndex = _NextSiblingIndex;
      _NextSiblingIndex = siblingIndex + 1;

      return new ChildResult<int>(new NodeAndSiblingIndex<int>(childIndex, siblingIndex));
    }

    public void Dispose()
    {
    }
  }
}
