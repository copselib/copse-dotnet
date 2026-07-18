namespace Copse.Linq
{
  // A buffer's storage encoding -- STORAGE vocabulary, deliberately distinct from
  // TreeTraversalStrategy (the TRAVERSAL vocabulary), per the naming rule: traversal things
  // speak dimensions, storage things speak encodings. The two map one-to-one (a preorder
  // layout replays depth-first natively; level-order, breadth-first) but they are not the
  // same concept: a strategy is how you WALK, a layout is how a capture is SHAPED.
  //
  // ONE neutral enum, not a codegen pair: pure vocabulary values have no color, and both
  // colors' buffer interfaces surface the same type (cross-color code compares them freely).
  // Lives in Copse.Linq.Traversal, the Linq-level neutral project, like MergeNode.
  public enum BufferLayout
  {
    Preorder,
    LevelOrder,
  }
}
