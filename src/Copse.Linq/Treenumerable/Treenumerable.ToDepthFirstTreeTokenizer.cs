using Copse.Core;
using Copse.Linq.TreeTokenizer.DepthFirstTree;

namespace Copse.Linq
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
