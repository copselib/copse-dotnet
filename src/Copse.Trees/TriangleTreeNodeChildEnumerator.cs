using Copse.Core;

namespace Copse.Trees
{
  public struct TriangleTreeNodeChildEnumerator
    : IChildEnumerator<int>
  {
    public TriangleTreeNodeChildEnumerator(int childCount)
    {
      _ChildCount = childCount;
      _ChildIndex = 0;
      _Disposed = false;
    }

    private readonly int _ChildCount;
    private int _ChildIndex;

    public Option<HandleAndSiblingIndex<int>> MoveNext()
    {
      if (_Disposed || _ChildIndex == _ChildCount)
      {
        return default;
      }

      var child = new HandleAndSiblingIndex<int>(_ChildIndex, _ChildIndex);
      _ChildIndex++;
      return new Option<HandleAndSiblingIndex<int>>(child);
    }

    private bool _Disposed;

    public void Dispose()
    {
      _Disposed = true;
    }
  }
}
