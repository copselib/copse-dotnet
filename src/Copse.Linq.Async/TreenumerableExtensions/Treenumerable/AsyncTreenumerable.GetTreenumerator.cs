using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    public static IAsyncTreenumerator<TNode> GetAsyncTreenumerator<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      return
        treeTraversalStrategy == TreeTraversalStrategy.BreadthFirst
        ? source.GetAsyncBreadthFirstTreenumerator()
        : source.GetAsyncDepthFirstTreenumerator();
    }
  }
}
