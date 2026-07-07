using Copse.Traversal;

namespace Copse.Benchmarks.Trees
{
  public struct DeepTreeNodeChildEnumerator
    : IChildCursor<int>
  {
    public DeepTreeNodeChildEnumerator(int ancestorCount)
    {
      _AncestorCount = ancestorCount;
    }

    private int _AncestorCount;

    public ChildResult<int> MoveNext()
    {
      if (_AncestorCount == 0)
        return default;

      var child = new NodeAndSiblingIndex<int>(_AncestorCount, 0);
      _AncestorCount = 0;
      return new ChildResult<int>(child);
    }

    public void Dispose()
    {
      // Do nothing.
    }
  }
}
