namespace Copse.Core.Async
{
  /// <summary>
  /// Async analog of <c>ITreenumerable</c>: the pure composite of the two traversal-dimension
  /// interfaces -- an async tree that affordably offers BOTH traversal streams. Sources that afford
  /// only one dimension (e.g. a forward-only async serialized stream) implement the matching narrow
  /// interface (<see cref="IAsyncDepthFirstTreenumerable{TNode}"/> or
  /// <see cref="IAsyncBreadthFirstTreenumerable{TNode}"/>) instead.
  ///
  /// <para>Looking for ADJACENCY -- parents, children, durable node handles? Streams have no
  /// addresses (a position exists only while its visit passes), so navigation lives on the
  /// CAPTURE: <c>Materialize()</c> returns a buffer that is also an
  /// <see cref="Copse.Async.IAsyncWalkableTreenumerable{TValue, THandle}"/>, and handles, walkers, and the
  /// probe surface start there. The escalation is deliberate -- the O(n) is disclosed, never
  /// hidden. See design-docs/WALKABLE_CONTRACT_DESIGN.md.</para>
  /// </summary>
  public interface IAsyncTreenumerable<TNode>
    : IAsyncDepthFirstTreenumerable<TNode>,
      IAsyncBreadthFirstTreenumerable<TNode>
  {
  }
}
