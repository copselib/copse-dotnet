using Copse.Core;
using Copse.Linq.Experimental.TreeTokenizer.DepthFirstTree;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    public static IDepthFirstTreeTokenizer<TNode> ToDepthFirstTreeTokenizer<TNode>(
      this IDepthFirstTreenumerable<TNode> source)
    {
      return new DepthFirstTreeTokenizer<TNode>(source);
    }
  }
}
