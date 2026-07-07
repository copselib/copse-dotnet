namespace Copse
{
  /// <summary>
  /// The result of a child pull: whether a child was yielded and, if so, which one. A named result
  /// struct (not a tuple) so the contract is self-documenting: when <see cref="HasChild"/> is false,
  /// <see cref="Child"/> is <c>default</c> and must not be read.
  ///
  /// <para>Returned BY VALUE from a child pull -- the shape that unifies the sync
  /// (<c>IChildCursor.MoveNext()</c>) and async (<c>IAsyncChildEnumerator.MoveNextAsync()</c>) pulls:
  /// it stores nothing between pulls (so the enumerator struct stays small -- no frame bloat, unlike a
  /// stored <c>Current</c>) AND it is legal in an async method (unlike an <c>out</c> param).</para>
  /// </summary>
  public readonly struct ChildResult<TNode>
  {
    public ChildResult(NodeAndSiblingIndex<TNode> child)
    {
      HasChild = true;
      Child = child;
    }

    public readonly bool HasChild;
    public readonly NodeAndSiblingIndex<TNode> Child;
  }
}
