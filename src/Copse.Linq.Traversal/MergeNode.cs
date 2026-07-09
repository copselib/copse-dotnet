namespace Copse.Linq.Treenumerators
{
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
