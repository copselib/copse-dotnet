using Copse;
using Copse.Traversal;

namespace Copse.Linq.Tests
{
  // Struct-return (by-value) adapter over the out-style PreorderChildEnumerator, so a generated cursor
  // driver can run over the same flat pre-order source the conformance oracle uses. Shared by the DFS
  // and BFS generated-engine conformance suites.
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
