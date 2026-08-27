namespace Copse.Linq
{
  /// <summary>
  /// Where an expansion's SLOT sits -- the position at which the substituted node's own
  /// children (each already rewritten) re-hang. Every placement here is lookahead-free over
  /// a depth-first visit stream: the children come LAST in the expansion's emission, so
  /// nothing of the expansion is owed after the source node's subtree ends (which a visit
  /// stream cannot announce without reading one event past it).
  /// </summary>
  public enum SlotPlacement
  {
    /// <summary>No slot: the expansion's forest stands alone and the node's children are
    /// dropped, never pulled (the vanish rule; PruneSubtreesWhere/PruneDescendantsWhere territory).
    /// <c>default</c> -- the least-capable value.</summary>
    None = 0,

    /// <summary>The slot follows the expansion's roots: the children become trailing roots
    /// beside them, in the same position the substituted node held. On an empty forest
    /// this is promotion -- Where's drop arm.</summary>
    AfterRoots,

    /// <summary>The slot is under the expansion's last root, after that root's own
    /// children (the Data.Tree order): <c>Return</c>'s rule. On an empty forest, the same
    /// as <see cref="AfterRoots"/>.</summary>
    UnderLastRoot,
  }
}
