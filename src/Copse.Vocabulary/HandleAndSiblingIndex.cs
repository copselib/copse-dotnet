namespace Copse
{
  /// <summary>
  /// A node's handle paired with the node's zero-based index among its siblings -- the shape
  /// both child-access protocols answer with: a child enumerator's pull yields one, and a
  /// topology's indexed child and root probes answer with one.
  /// </summary>
  public readonly struct HandleAndSiblingIndex<THandle>
  {
    /// <summary>Pairs <paramref name="handle"/> with <paramref name="siblingIndex"/>.</summary>
    public HandleAndSiblingIndex(
      THandle handle,
      int siblingIndex)
    {
      Handle = handle;
      SiblingIndex = siblingIndex;
    }

    /// <summary>The node's handle.</summary>
    public readonly THandle Handle;

    /// <summary>The node's zero-based index among its siblings.</summary>
    public readonly int SiblingIndex;

    public override string ToString() => $"{SiblingIndex}  {Handle}";
  }
}
