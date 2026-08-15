namespace Copse.Core
{
  // Pure composite of the two traversal-dimension interfaces: an ITreenumerable is a tree
  // that affordably offers BOTH traversal streams. Sources that can only afford one
  // dimension (e.g. a forward-only serialized stream) implement the matching narrow
  // interface instead; Memoize/Materialize are the explicit upgrade back to the composite.
  // See design-docs/TRAVERSAL_DIMENSION_SPLIT.md.
  //
  // Looking for ADJACENCY -- parents, children, durable node handles? Streams have no
  // addresses (a position exists only while its visit passes), so navigation lives on the
  // CAPTURE: Materialize() returns a buffer that is also an IWalkableTreenumerable, and
  // handles, walkers, and the probe surface start there. The escalation is deliberate --
  // the O(n) is disclosed, never hidden. See design-docs/WALKABLE_CONTRACT_DESIGN.md.
  public interface ITreenumerable<TNode>
    : IDepthFirstTreenumerable<TNode>,
      IBreadthFirstTreenumerable<TNode>
  {
  }
}
