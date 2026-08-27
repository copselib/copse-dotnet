namespace Copse.Linq
{
  // A variant, not a pair of options: options over the sides would admit (none, none) -- a
  // node from neither tree, which no merge produces -- and would lose HasLeftAndRight, the
  // state the domain has a word for.
  /// <summary>
  /// One node of a merged tree, with its provenance: exactly one of three states holds --
  /// contributed by the left tree only, the right tree only, or both. Read
  /// <see cref="HasLeft"/>/<see cref="HasRight"/> before the matching side's value; the
  /// absent side's value is <c>default</c> and must not be read.
  /// </summary>
  public readonly struct MergeNode<TLeft, TRight>
  {
    /// <summary>Creates a merge node from its sides and their presence flags.</summary>
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

    /// <summary>The left tree's value; valid only when <see cref="HasLeft"/>.</summary>
    public TLeft Left { get; }

    /// <summary>The right tree's value; valid only when <see cref="HasRight"/>.</summary>
    public TRight Right { get; }

    /// <summary>Whether the left tree contributed this node.</summary>
    public bool HasLeft { get; }

    /// <summary>Whether the right tree contributed this node.</summary>
    public bool HasRight { get; }

    /// <summary>Whether both trees contributed this node -- the merged case.</summary>
    public bool HasLeftAndRight => HasLeft && HasRight;

    /// <inheritdoc/>
    public override string ToString() => $"({Left}, {Right})";
  }
}
