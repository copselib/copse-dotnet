using Copse.Core;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static int CountTrees<TNode>(this IDepthFirstTreenumerable<TNode> source)
      => source.GetRoots().Count();
  }
}
