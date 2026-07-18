using Copse.Benchmarks.Trees;
using Copse.Core;
using Copse.Linq;
using Copse.Trees;

namespace Copse.Benchmarks
{
  internal static class Treenumerables
  {
    public static ITreenumerable<int> GetDeepTree(int width) => new DeepTree(width);

    public static ITreenumerable<int> GetWideTree(int depth) =>
      new CompleteBinaryTree()
      .PruneBefore((n, position) => position.Depth == depth);

    public static ITreenumerable<int> GetTree(int cutoff, TreeShape treeShape)
    {
      return
        treeShape == TreeShape.Deep
        ? GetDeepTree(cutoff)
        : GetWideTree(cutoff);
    }
  }
}
