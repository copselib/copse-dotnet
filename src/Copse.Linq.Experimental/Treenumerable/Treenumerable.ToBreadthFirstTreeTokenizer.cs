using Copse.Core;
using Copse.Linq.Experimental.TreeTokenizer.BreadthFirstTree;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    public static IBreadthFirstTreeTokenizer<TNode> ToBreadthFirstTreeTokenizer<TNode>(
      this IBreadthFirstTreenumerable<TNode> source)
    {
      return new BreadthFirstTreeTokenizer<TNode>(source);
    }
  }
}
