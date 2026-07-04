namespace Copse.Core
{
  // Pure composite of the two traversal-dimension interfaces: an ITreenumerable is a tree
  // that affordably offers BOTH traversal streams. Sources that can only afford one
  // dimension (e.g. a forward-only serialized stream) implement the matching narrow
  // interface instead; Memoize/Materialize are the explicit upgrade back to the composite.
  // See docs/TRAVERSAL_DIMENSION_SPLIT.md.
  public interface ITreenumerable<TNode>
    : IDepthFirstTreenumerable<TNode>,
      IBreadthFirstTreenumerable<TNode>
  {
  }
}
