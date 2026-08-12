namespace Copse.Linq
{
  /// <summary>
  /// The result of a cursor step: whether the step found a node and, if so, the cursor
  /// standing there. The walker tier's member of the named-result-struct family
  /// (<see cref="Copse.ChildResult{TNode}"/>, <see cref="Copse.ParentResult{THandle}"/>):
  /// returned BY VALUE, and when <see cref="HasCursor"/> is false, <see cref="Cursor"/> is
  /// <c>default</c> and must not be read. This is what keeps the no-unfocused-cursor
  /// invariant honest -- a failed step yields NO cursor, never a cursor standing nowhere.
  /// </summary>
  public readonly struct TreeCursorResult<TValue, THandle>
  {
    public TreeCursorResult(TreeCursor<TValue, THandle> cursor)
    {
      HasCursor = true;
      Cursor = cursor;
    }

    public readonly bool HasCursor;
    public readonly TreeCursor<TValue, THandle> Cursor;
  }
}
