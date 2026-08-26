namespace Copse
{
  /// <summary>
  /// A node's handle paired with the node itself. This is what the handle-acquisition scans
  /// (such as <c>GetHandlesWithValues</c>) yield, so a predicate over nodes can pick out the
  /// handles it wants.
  /// </summary>
  public readonly struct HandleAndNode<THandle, TNode>
  {
    /// <summary>Pairs <paramref name="handle"/> with <paramref name="node"/>.</summary>
    public HandleAndNode(THandle handle, TNode node)
    {
      Handle = handle;
      Node = node;
    }

    /// <summary>The node's handle, valid in the source that produced it.</summary>
    public readonly THandle Handle;

    /// <summary>The node.</summary>
    public readonly TNode Node;

    public override string ToString() => $"{Handle}  {Node}";
  }
}
