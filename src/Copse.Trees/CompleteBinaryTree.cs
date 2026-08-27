using Copse.Treenumerables;
using Copse.Core;

namespace Copse.Trees
{
  /// <summary>The complete binary tree: node n's children are 2n+1 and 2n+2. Infinite; bound
  /// it with a prune or a bounded child enumerator.</summary>
  public class CompleteBinaryTree : ITreenumerable<int>
  {
    // Constructed directly (not through the Tree.Create door): the corpus trees pin the
    // ENGINE -- benchmark rows and conformance suites measure it through them.
    private readonly ITreenumerable<int> _Tree =
      new HierarchicalTreenumerable<int, CompleteBinaryTreeNodeChildEnumerator>(
        nodeContext => new CompleteBinaryTreeNodeChildEnumerator(nodeContext.Node),
        new int[] { 0 });

    /// <inheritdoc/>
    public ITreenumerator<int> GetDepthFirstTreenumerator() => _Tree.GetDepthFirstTreenumerator();

    /// <inheritdoc/>
    public ITreenumerator<int> GetBreadthFirstTreenumerator() => _Tree.GetBreadthFirstTreenumerator();
  }
}
