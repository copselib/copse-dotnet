using Copse.Traversal;

namespace Copse.Trees
{
  public struct CompleteBinaryTreeNodeChildEnumerator
    : IChildCursor<int>
  {
    public CompleteBinaryTreeNodeChildEnumerator(int parentValue)
    {
      try
      {
        _ChildValue = checked(parentValue * 2);
      }
      catch
      {
        _ChildValue = int.MaxValue;
      }
      _Disposed = false;
    }

    private void TryIncrementChildValue()
    {
      if (_ChildValue == int.MaxValue)
        return;

      if ((_ChildValue & 1) == 1)
      {
        _ChildValue = int.MaxValue;
        return;
      }

      try
      {
        _ChildValue = checked(_ChildValue + 1);
      }
      catch
      {
        _ChildValue = int.MaxValue;
      }
    }

    private int _ChildValue;

    public ChildResult<int> MoveNext()
    {
      if (_Disposed || _ChildValue == int.MaxValue)
      {
        return default;
      }

      var child = new NodeAndSiblingIndex<int>(_ChildValue, (int)(_ChildValue % 2));

      TryIncrementChildValue();

      return new ChildResult<int>(child);
    }

    private bool _Disposed;

    public void Dispose()
    {
      _Disposed = true;
    }
  }
}
