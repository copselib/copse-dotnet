using System;

namespace Copse.Core
{
  /// <summary>
  /// Flags passed to a treenumerator's <c>MoveNext</c> to prune the traversal from the current
  /// node: skip the rest of this node's visits, its descendants, its later siblings, or any
  /// combination. Pass <see cref="TraverseAll"/> to prune nothing. The flags act on the node
  /// the treenumerator is currently standing on and take effect from that point forward.
  /// </summary>
  [Flags]
  public enum NodeTraversalStrategies
  {
    /// <summary>Prune nothing; continue the full traversal.</summary>
    TraverseAll                 = 0,

    /// <summary>Emit no further visits for the current node. Its descendants are still
    /// traversed, and their positions are unchanged.</summary>
    SkipNode                    = 1,

    /// <summary>Do not traverse the current node's descendants.</summary>
    PruneDescendants            = 2,

    /// <summary>Skip the current node's remaining visits and its descendants.</summary>
    PruneSubtree                = SkipNode | PruneDescendants,

    /// <summary>Do not schedule the current node's later siblings.</summary>
    PruneSiblings               = 4,

    /// <summary>Skip the current node's remaining visits and its later siblings.</summary>
    SkipNodeAndPruneSiblings    = SkipNode | PruneSiblings,

    /// <summary>Skip the current node's descendants and its later siblings.</summary>
    PruneDescendantsAndSiblings = PruneDescendants | PruneSiblings,

    /// <summary>Skip everything reachable from the current node: its remaining visits, its
    /// descendants, and its later siblings.</summary>
    PruneSubtreeAndSiblings     = SkipNode | PruneDescendants | PruneSiblings,
  }
}
