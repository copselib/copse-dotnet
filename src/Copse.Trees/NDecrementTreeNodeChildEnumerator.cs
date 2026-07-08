namespace Copse.Trees
{
  public struct NDecrementTreeNodeChildEnumerator
    : IChildEnumerator<int>
  {
    public NDecrementTreeNodeChildEnumerator(int depth)
    {
      _Depth = depth;
      _Disposed = false;
    }

    private int _Depth;

    public ChildResult<int> MoveNext()
    {
      if (_Disposed || _Depth == 0)
      {
        return default;
      }

      _Depth--;
      var child = new NodeAndSiblingIndex<int>(_Depth, 0);
      return new ChildResult<int>(child);
    }

    private bool _Disposed;

    public void Dispose()
    {
      _Disposed = true;
    }
  }
}
