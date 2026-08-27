using Copse.Core;

namespace Copse
{
  /// <summary>
  /// A node paired with its position -- the shape positional predicates and selectors receive,
  /// so one lambda parameter carries both the value and where it sits.
  /// </summary>
  public readonly struct NodeAndPosition<TNode>
  {
    /// <summary>Pairs <paramref name="node"/> with <paramref name="position"/>.</summary>
    public NodeAndPosition(
      TNode node,
      NodePosition position)
    {
      Node = node;
      Position = position;
    }

    /// <summary>The node at this position in the tree.</summary>
    public readonly TNode Node;

    /// <summary>The node's position (sibling index and depth).</summary>
    public readonly NodePosition Position;

    /// <inheritdoc/>
    public override string ToString() => $"{Position}  {Node}";
  }
}
