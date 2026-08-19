
namespace Copse.Benchmarks.Trees
{
  public struct DeepTreeNodeChildEnumerator
    : IChildEnumerator<int>
  {
    public DeepTreeNodeChildEnumerator(int ancestorCount)
    {
      _AncestorCount = ancestorCount;
    }

    private int _AncestorCount;

    public Option<NodeAndSiblingIndex<int>> MoveNext()
    {
      if (_AncestorCount == 0)
        return default;

      var child = new NodeAndSiblingIndex<int>(_AncestorCount, 0);
      _AncestorCount = 0;
      return new Option<NodeAndSiblingIndex<int>>(child);
    }

    public void Dispose()
    {
      // Do nothing.
    }
  }
}
