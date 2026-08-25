namespace Copse
{
  /// <summary>
  /// A node paired with its zero-based index among its siblings. This is the shape a child
  /// enumerator yields: each pulled child arrives together with its position in the family.
  /// </summary>
  public readonly struct NodeAndSiblingIndex<TNode>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="siblingIndex"/>.</summary>
    public NodeAndSiblingIndex(
      TNode node,
      int siblingIndex)
    {
      Node = node;
      SiblingIndex = siblingIndex;
    }

    /// <summary>The node.</summary>
    public readonly TNode Node;

    /// <summary>The node's zero-based index among its siblings.</summary>
    public readonly int SiblingIndex;

    public override string ToString() => $"{SiblingIndex}  {Node}";
  }
}
