using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Drops the first <paramref name="count"/> root trees (prunes them before their first visit). Deferred.</summary>
    public static IAsyncTreenumerable<TNode> SkipTrees<TNode>(
      this IAsyncTreenumerable<TNode> source,
      int count)
      => source.PruneSubtreesWhere((node, position) => position.Depth == 0 && position.SiblingIndex < count);

    /// <summary>Drops the first <paramref name="count"/> root trees (prunes them before their first visit). Deferred.</summary>
    public static IAsyncDepthFirstTreenumerable<TNode> SkipTrees<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      int count)
      => source.PruneSubtreesWhere((node, position) => position.Depth == 0 && position.SiblingIndex < count);

    /// <summary>Drops the first <paramref name="count"/> root trees (prunes them before their first visit). Deferred.</summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> SkipTrees<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      int count)
      => source.PruneSubtreesWhere((node, position) => position.Depth == 0 && position.SiblingIndex < count);
  }
}
