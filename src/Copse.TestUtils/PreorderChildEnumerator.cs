using Copse.Traversal;

namespace Copse
{
  // Enumerates the children of a node in a flat pre-order tree (see PreorderTree). A node's
  // children occupy the contiguous span (i, i + subtreeSizes[i]); we hop over each child's
  // whole subtree to land on the next child. A struct so the engine holds it unboxed, in place.
  public struct PreorderChildEnumerator : IChildCursor<int>
  {
    public PreorderChildEnumerator(int[] subtreeSizes, int parentIndex)
    {
      _SubtreeSizes = subtreeSizes;
      _Cursor = parentIndex + 1;
      _End = parentIndex + subtreeSizes[parentIndex];
      _SiblingIndex = 0;
      _Disposed = false;
    }

    private readonly int[] _SubtreeSizes;
    private readonly int _End;
    private int _Cursor;
    private int _SiblingIndex;
    private bool _Disposed;

    public ChildResult<int> MoveNext()
    {
      // Dispose() is how the engine signals SkipDescendants/SkipSiblings: once disposed we must
      // yield no further children (see TriangleTreeNodeChildEnumerator for the same contract).
      if (_Disposed || _Cursor >= _End)
        return default;

      var child = new NodeAndSiblingIndex<int>(_Cursor, _SiblingIndex);
      _SiblingIndex++;
      _Cursor += _SubtreeSizes[_Cursor]; // skip the child's whole subtree
      return new ChildResult<int>(child);
    }

    public void Dispose() => _Disposed = true;
  }
}
