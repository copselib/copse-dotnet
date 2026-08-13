namespace Copse.Async
{
  /// <summary>
  /// The result of a walker step: whether the step found a node and, if so, the walker
  /// standing there. The walker tier's member of the named-result-struct family
  /// (<see cref="ChildResult{TNode}"/>, <see cref="ParentResult{THandle}"/>): returned BY
  /// VALUE, and when <see cref="HasWalker"/> is false, <see cref="Walker"/> is <c>default</c>
  /// and must not be read. This is what keeps the no-unfocused-walker invariant honest -- a
  /// failed step yields NO walker, never a walker standing nowhere.
  /// </summary>
  public readonly struct AsyncTreeWalkerResult<TValue, THandle>
  {
    public AsyncTreeWalkerResult(AsyncTreeWalker<TValue, THandle> walker)
    {
      HasWalker = true;
      Walker = walker;
    }

    public readonly bool HasWalker;
    public readonly AsyncTreeWalker<TValue, THandle> Walker;
  }
}
