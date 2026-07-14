namespace Copse.Linq.Async.Treenumerables
{
  // A buffer's storage encoding -- STORAGE vocabulary, deliberately distinct from
  // TreeTraversalStrategy (the TRAVERSAL vocabulary), per the naming rule: traversal things
  // speak dimensions, storage things speak encodings. The two map one-to-one (a preorder
  // layout replays depth-first natively; level-order, breadth-first) but they are not the
  // same concept: a strategy is how you WALK, a layout is how a capture is SHAPED.
  public enum BufferLayout
  {
    Preorder,
    LevelOrder,
  }
}
