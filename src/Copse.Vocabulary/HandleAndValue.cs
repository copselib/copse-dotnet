namespace Copse
{
  /// <summary>
  /// A node's handle paired with its value. This is what the handle-acquisition scans (such as
  /// <c>GetHandlesWithValues</c>) yield, so a predicate over values can pick out the handles it
  /// wants.
  /// </summary>
  public readonly struct HandleAndValue<THandle, TValue>
  {
    /// <summary>Pairs <paramref name="handle"/> with <paramref name="value"/>.</summary>
    public HandleAndValue(THandle handle, TValue value)
    {
      Handle = handle;
      Value = value;
    }

    /// <summary>The node's handle, valid in the source that produced it.</summary>
    public readonly THandle Handle;

    /// <summary>The node's value.</summary>
    public readonly TValue Value;

    public override string ToString() => $"{Handle}  {Value}";
  }
}
