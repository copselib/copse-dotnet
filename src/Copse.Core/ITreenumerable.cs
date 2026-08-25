namespace Copse.Core
{
  /// <summary>
  /// A tree that offers both traversal orders: the composite of
  /// <see cref="IDepthFirstTreenumerable{TNode}"/> and
  /// <see cref="IBreadthFirstTreenumerable{TNode}"/>, adding no members of its own. Sources
  /// that can only afford one order (for example, a forward-only serialized stream) implement
  /// just the matching narrow interface, so asking them for the other order is a compile
  /// error rather than a hidden cost; <c>Memoize</c> and <c>Materialize</c> upgrade a narrow
  /// source back to this composite.
  ///
  /// <para>For navigation -- parents, children, durable node handles -- capture first: a
  /// traversal stream has no addresses (a position exists only while its visit passes), so
  /// <c>Materialize()</c> returns a buffer that is also walkable, and handles, walkers, and
  /// the adjacency probes start there.</para>
  /// </summary>
  public interface ITreenumerable<TNode>
    : IDepthFirstTreenumerable<TNode>,
      IBreadthFirstTreenumerable<TNode>
  {
  }
}
