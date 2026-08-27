namespace Copse.Core
{
  /// <summary>Which traversal order to walk a tree in.</summary>
  public enum TreeTraversalStrategy
  {
    /// <summary>Level by level: all nodes at one depth are scheduled before any node at the
    /// next depth is visited.</summary>
    BreadthFirst,

    /// <summary>Subtree by subtree: each child's entire subtree completes before its next
    /// sibling is scheduled.</summary>
    DepthFirst
  }
}
