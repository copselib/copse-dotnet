using Copse.Treenumerables;
using System.Linq;

namespace Copse.Benchmarks.Trees
{
  public class DeepTree : Treenumerable<int, DeepTreeNodeChildEnumerator>
  {
    public DeepTree(int width)
      : base(
          nodeContext => new DeepTreeNodeChildEnumerator(nodeContext.Node - 1),
          EnumerableExtensions.Geometric(1, 2).Take(width))
    {
    }
  }
}
