using Copse.Core;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Acquires a traversal in the named dimension. The caller owns the
    /// treenumerator and disposes it.</summary>
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
