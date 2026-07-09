using Copse.Core.Async;
using Copse.Linq.Async.TreeTokenizer.BreadthFirstTree;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    public static IAsyncBreadthFirstTreeTokenizer<TNode> ToBreadthFirstTreeTokenizer<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      return new AsyncBreadthFirstTreeTokenizer<TNode>(source);
    }
  }
}
