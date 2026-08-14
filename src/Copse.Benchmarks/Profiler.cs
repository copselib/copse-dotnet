using Copse.Linq;
using System.Linq;

namespace Copse.Benchmarks
{
  public class Profiler
  {
    public int LevelOrderTraversal_DeepTree()
    {
      var tree = Treenumerables.GetDeepTree(19);

      return
        tree
        .GetLevelOrderTraversal()
        .Count();
    }
  }
}
