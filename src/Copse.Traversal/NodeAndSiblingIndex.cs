namespace Copse
{
  public readonly struct NodeAndSiblingIndex<TNode>
  {
    public NodeAndSiblingIndex(
      TNode node,
      int siblingIndex)
    {
      Node = node;
      SiblingIndex = siblingIndex;
    }

    public readonly TNode Node;
    public readonly int SiblingIndex;

    public override string ToString() => $"{SiblingIndex}  {Node}";
  }
}
