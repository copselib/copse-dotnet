using Copse.Treenumerables;
using Copse.Core;

namespace Copse.Trees
{
  /// <summary>The triangle tree: level width grows linearly with depth. Infinite; bound it
  /// with a prune or a bounded child enumerator.</summary>
  public class TriangleTree : ITreenumerable<int>
  {
    // Constructed directly (not through the Tree.Create door): the corpus trees pin the
    // ENGINE -- benchmark rows and conformance suites measure it through them.
    private readonly ITreenumerable<int> _Tree =
      new HierarchicalTreenumerable<int, TriangleTreeNodeChildEnumerator>(
        nodeContext => new TriangleTreeNodeChildEnumerator(nodeContext.Node == 0 ? nodeContext.Position.Depth + 2 : 0),
        new[] { 0 });

    /// <inheritdoc/>
    public ITreenumerator<int> GetDepthFirstTreenumerator() => _Tree.GetDepthFirstTreenumerator();

    /// <inheritdoc/>
    public ITreenumerator<int> GetBreadthFirstTreenumerator() => _Tree.GetBreadthFirstTreenumerator();
  }
}
