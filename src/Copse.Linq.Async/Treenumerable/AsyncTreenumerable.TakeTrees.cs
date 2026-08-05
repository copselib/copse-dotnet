using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Keeps only the first <paramref name="count"/> root trees (stops at the root whose sibling index reaches count). Deferred.</summary>
    public static IAsyncTreenumerable<TNode> TakeTrees<TNode>(
      this IAsyncTreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        (_, position) => position.Depth == 0 && position.SiblingIndex == count,
        false);

    public static IAsyncDepthFirstTreenumerable<TNode> TakeTrees<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        (_, position) => position.Depth == 0 && position.SiblingIndex == count,
        false);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeTrees<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      int count)
      => source.TakeNodesUntil(
        (_, position) => position.Depth == 0 && position.SiblingIndex == count,
        false);
  }
}
