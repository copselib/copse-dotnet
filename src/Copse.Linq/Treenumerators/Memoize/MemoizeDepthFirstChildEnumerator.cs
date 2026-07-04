namespace Copse.Linq.Treenumerators
{
  // Enumerates the children of a node in a memo's preorder (DFT) dimension buffer: the open-span
  // dual of PreorderChildEnumerator. That type precomputes its end bound from the parent's
  // subtree size in its constructor; here the parent's subtree may still be OPEN (the feed
  // suspended somewhere inside it), so the bound is discovered on the fly, filling the buffer
  // only as far as each advance demands:
  //
  //  - Hop: the next candidate is the previous child's index plus its subtree size. In natural
  //    DFS replay the engine asks only after that child's subtree was just traversed -- fully
  //    buffered, so the fill is at most the one-node overshoot that records the close. A consumer
  //    skip-hop over an untraversed span is what pays: it buffers the skipped subtree, lazily --
  //    the eager-skip price (see MEMOIZE_DESIGN.md).
  //  - End test: a buffered candidate is the parent's next child iff the parent's subtree is
  //    still open (everything appended past a still-open node lies inside its span -- the feed
  //    has not left it) or the candidate falls short of the closed span's end. An unbuffered
  //    candidate after the fill gives up means the stream exhausted, which closes every span:
  //    no more children.
  //
  // The contract is engine-agnostic: DFS rides it natively; BFS rides the same enumerator
  // cross-order over a completed buffer. A struct so the engine holds it unboxed, in place;
  // Dispose() is how the engine signals SkipDescendants/SkipSiblings (see
  // PreorderChildEnumerator for the same contract).
  internal struct MemoizeDepthFirstChildEnumerator<TValue> : IChildEnumerator<int>
  {
    public MemoizeDepthFirstChildEnumerator(MemoizeDepthFirstBuffer<TValue> buffer, int parentIndex)
    {
      _Buffer = buffer;
      _ParentIndex = parentIndex;
      _CurrentChild = -1;
      _SiblingIndex = 0;
      _Disposed = false;
    }

    private readonly MemoizeDepthFirstBuffer<TValue> _Buffer;
    private readonly int _ParentIndex;
    private int _CurrentChild; // -1 = no child yielded yet
    private int _SiblingIndex;
    private bool _Disposed;

    public bool MoveNext(out NodeAndSiblingIndex<int> childNodeAndSiblingIndex)
    {
      if (_Disposed)
      {
        childNodeAndSiblingIndex = default;
        return false;
      }

      var candidate = _CurrentChild < 0
        ? _ParentIndex + 1
        : _CurrentChild + _Buffer.EnsureSubtreeClosed(_CurrentChild);

      if (!_Buffer.EnsureBuffered(candidate))
      {
        childNodeAndSiblingIndex = default;
        return false;
      }

      var parentSubtreeSize = _Buffer.GetSubtreeSize(_ParentIndex);

      if (parentSubtreeSize != 0 && candidate >= _ParentIndex + parentSubtreeSize)
      {
        childNodeAndSiblingIndex = default;
        return false;
      }

      _CurrentChild = candidate;
      childNodeAndSiblingIndex = new NodeAndSiblingIndex<int>(candidate, _SiblingIndex);
      _SiblingIndex++;
      return true;
    }

    public void Dispose() => _Disposed = true;
  }
}
