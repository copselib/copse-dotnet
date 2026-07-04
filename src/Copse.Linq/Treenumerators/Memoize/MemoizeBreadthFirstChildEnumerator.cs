namespace Copse.Linq.Treenumerators
{
  // Enumerates the children of a node in a memo's level-order (BFT) dimension buffer. Children
  // of one node are a contiguous level-order span, so the walk is just first-child + ordinal --
  // and the ordinal IS the sibling index. The buffer serves children of a still-open span as
  // they appear (EnsureChildAvailable), so only "no more children" ever waits on the feed.
  //
  // The contract is engine-agnostic: BFS rides it natively; DFS rides the same enumerator
  // cross-order over a completed buffer. A struct so the engine holds it unboxed, in place;
  // Dispose() is how the engine signals SkipDescendants/SkipSiblings (see
  // PreorderChildEnumerator for the same contract).
  internal struct MemoizeBreadthFirstChildEnumerator<TValue> : IChildEnumerator<int>
  {
    public MemoizeBreadthFirstChildEnumerator(MemoizeBreadthFirstBuffer<TValue> buffer, int parentIndex)
    {
      _Buffer = buffer;
      _ParentIndex = parentIndex;
      _NextOrdinal = 0;
      _Disposed = false;
    }

    private readonly MemoizeBreadthFirstBuffer<TValue> _Buffer;
    private readonly int _ParentIndex;
    private int _NextOrdinal;
    private bool _Disposed;

    public bool MoveNext(out NodeAndSiblingIndex<int> childNodeAndSiblingIndex)
    {
      if (_Disposed || !_Buffer.EnsureChildAvailable(_ParentIndex, _NextOrdinal))
      {
        childNodeAndSiblingIndex = default;
        return false;
      }

      var childIndex = _Buffer.GetFirstChildIndex(_ParentIndex) + _NextOrdinal;

      childNodeAndSiblingIndex = new NodeAndSiblingIndex<int>(childIndex, _NextOrdinal);
      _NextOrdinal++;
      return true;
    }

    public void Dispose() => _Disposed = true;
  }
}
