using Copse.Core;
using Copse.Linq.TreeTokenizer.BreadthFirstTree;

namespace Copse.Linq
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
