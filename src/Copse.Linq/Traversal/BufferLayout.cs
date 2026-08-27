namespace Copse.Linq
{
  // One neutral enum, not a codegen pair: pure vocabulary values have no color, and both
  // colors' buffer interfaces surface the same type.
  /// <summary>
  /// How a captured tree is stored. Distinct from <c>TreeTraversalStrategy</c>: a strategy is
  /// how you walk, a layout is how a capture is shaped. They map one-to-one for native
  /// replay -- a preorder capture replays depth-first natively, a level-order capture
  /// breadth-first -- and each layout still serves the other order, cross-decoded.
  /// </summary>
  public enum BufferLayout
  {
    /// <summary>Nodes stored in depth-first (preorder) order.</summary>
    Preorder,

    /// <summary>Nodes stored level by level.</summary>
    LevelOrder,
  }
}
