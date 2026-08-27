using Copse.Core;
using Copse.Treenumerables;
using System.Linq;

namespace Copse.Benchmarks.Trees
{
  public class DeepTree : ITreenumerable<int>
  {
    private readonly ITreenumerable<int> _Tree;

    public DeepTree(int width)
    {
      // Constructed directly (not through the Tree.Create door): the corpus trees pin the
      // ENGINE -- the Traversal rows measure it through them.
      _Tree = new HierarchicalTreenumerable<int, DeepTreeNodeChildEnumerator>(
        nodeAndPosition => new DeepTreeNodeChildEnumerator(nodeAndPosition.Node - 1),
        EnumerableExtensions.Geometric(1, 2).Take(width));
    }

    public ITreenumerator<int> GetDepthFirstTreenumerator() => _Tree.GetDepthFirstTreenumerator();

    public ITreenumerator<int> GetBreadthFirstTreenumerator() => _Tree.GetBreadthFirstTreenumerator();
  }
}
