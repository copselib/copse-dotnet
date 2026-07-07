using Copse;
using Copse.Traversal;

namespace Copse.Linq.Tests
{
  // Current-style adapter over the out-style PreorderChildEnumerator, so a generated (Current-style)
  // driver can run over the same flat pre-order source the conformance oracle uses. Shared by the DFS
  // and BFS generated-engine conformance suites.
  internal struct ForwardPreorderChildEnumerator : IForwardChildEnumerator<int>
  {
    private PreorderChildEnumerator _inner;
    private NodeAndSiblingIndex<int> _current;

    public ForwardPreorderChildEnumerator(int[] subtreeSizes, int parentIndex)
    {
      _inner = new PreorderChildEnumerator(subtreeSizes, parentIndex);
      _current = default;
    }

    public bool MoveNext()
    {
      if (_inner.MoveNext(out var child))
      {
        _current = child;
        return true;
      }
      return false;
    }

    public NodeAndSiblingIndex<int> Current => _current;

    public void Dispose() => _inner.Dispose();
  }

  // Struct-return (by-value) adapter over the out-style PreorderChildEnumerator, for the cursor driver.
  internal struct PreorderChildCursor : IChildCursor<int>
  {
    private PreorderChildEnumerator _inner;

    public PreorderChildCursor(int[] subtreeSizes, int parentIndex)
      => _inner = new PreorderChildEnumerator(subtreeSizes, parentIndex);

    public ChildResult<int> MoveNext()
      => _inner.MoveNext(out var child) ? new ChildResult<int>(child) : default;

    public void Dispose() => _inner.Dispose();
  }
}
