namespace Copse.Linq.Treenumerators
{
  // One node of a merged tree, with the provenance the merge assigned it: which side or sides
  // contributed it. A VARIANT, not a pair of options -- the node always exists, so the flags do
  // not say "this value is absent", they say which of the three states holds (left only, right
  // only, both). Options over the sides would admit a fourth, (none, none): a node from neither
  // tree, which no merge produces, and would lose HasLeftAndRight, the state the domain has a
  // word for. A discriminated union is what this wants to be; C# has none, so the flags carry
  // the invariant.
  public readonly struct MergeNode<TLeft, TRight>
  {
    public MergeNode(
      TLeft left,
      TRight right,
      bool hasLeft,
      bool hasRight)
    {
      Left = left;
      Right = right;
      HasLeft = hasLeft;
      HasRight = hasRight;
    }

    public TLeft Left { get; }
    public TRight Right { get; }
    public bool HasLeft { get; }
    public bool HasRight { get; }
    public bool HasLeftAndRight => HasLeft && HasRight;

    public override string ToString() => $"({Left}, {Right})";
  }
}
