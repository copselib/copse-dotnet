using Copse.Core.Async;
using Copse.Linq.Async.TreeTokenizer.DepthFirstTree;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    public static IAsyncDepthFirstTreeTokenizer<TNode> ToDepthFirstTreeTokenizer<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      return new AsyncDepthFirstTreeTokenizer<TNode>(source);
    }
  }
}
