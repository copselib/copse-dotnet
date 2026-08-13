using Copse.Async;
namespace Copse.Linq
{
  /// <summary>
  /// The result of a single-handle search: whether a match was found and, if so, its handle.
  /// The acquisition surface's member of the named-result-struct family
  /// (<see cref="ChildResult{TNode}"/>, <see cref="ParentResult{THandle}"/>): returned BY
  /// VALUE, and when <see cref="HasHandle"/> is false, <see cref="Handle"/> is <c>default</c>
  /// and must not be read.
  ///
  /// <para>This struct exists because the miss is otherwise UNREPRESENTABLE as a handle:
  /// ordinal handle spaces start at zero, so <c>FirstOrDefault()</c> over handles returns a
  /// REAL node (the first one) on a miss -- the sentinel collision. A result struct makes the
  /// miss a fact, not a default.</para>
  /// </summary>
  public readonly struct HandleResult<THandle>
  {
    public HandleResult(THandle handle)
    {
      HasHandle = true;
      Handle = handle;
    }

    public readonly bool HasHandle;
    public readonly THandle Handle;
  }
}
