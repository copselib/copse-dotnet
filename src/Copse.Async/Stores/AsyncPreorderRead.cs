namespace Copse.Stores
{
  /// <summary>
  /// What one forward-only preorder read yields: a node and the depth it sits at. Whether
  /// there was a read at all is carried by the <see cref="Option{TValue}"/> wrapping this --
  /// an exhausted stream answers absent.
  /// </summary>
  public readonly struct AsyncPreorderRead<TNode>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="depth"/>.</summary>
    public AsyncPreorderRead(TNode node, int depth)
    {
      Node = node;
      Depth = depth;
    }

    /// <summary>The read's node -- meaningful only when the wrapping option is present.</summary>
    public readonly TNode Node;

    /// <summary>The node's depth (roots are depth 0).</summary>
    public readonly int Depth;
  }
}
