namespace Copse
{
  /// <summary>
  /// A node's handle paired with the node's zero-based index among its siblings -- the shape
  /// both child-access protocols answer with: a child enumerator's pull yields one, and a
  /// topology's indexed child and root probes answer with one.
  /// </summary>
  public readonly struct NodeAndSiblingIndex<THandle>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="siblingIndex"/>.</summary>
    public NodeAndSiblingIndex(
      THandle node,
      int siblingIndex)
    {
      Node = node;
      SiblingIndex = siblingIndex;
    }

    /// <summary>The node's handle.</summary>
    public readonly THandle Node;

    /// <summary>The node's zero-based index among its siblings.</summary>
    public readonly int SiblingIndex;

    public override string ToString() => $"{SiblingIndex}  {Node}";
  }
}
