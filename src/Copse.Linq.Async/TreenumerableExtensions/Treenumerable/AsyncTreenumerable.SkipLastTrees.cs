using Copse.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Drops the last <paramref name="count"/> root trees. The root count is resolved first (the
    /// source is enumerated once to count, then again to take), so nothing streams until the
    /// count is known.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerable<TNode>> SkipLastTreesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      int count,
      CancellationToken cancellationToken = default)
    {
      var treeCount = await source.CountTreesAsync(cancellationToken).ConfigureAwait(false);

      var takeCount = Math.Max(treeCount - count, 0);

      return source.TakeTrees(takeCount);
    }

    /// <summary>The depth-first-narrow twin: the same two passes (count the roots, then take), staying narrow.</summary>
    public static async ValueTask<IAsyncDepthFirstTreenumerable<TNode>> SkipLastTreesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      int count,
      CancellationToken cancellationToken = default)
    {
      var treeCount = await source.CountTreesAsync(cancellationToken).ConfigureAwait(false);

      var takeCount = Math.Max(treeCount - count, 0);

      return source.TakeTrees(takeCount);
    }

    /// <summary>
    /// The breadth-first-narrow twin. The counting pass is nearly free in this dimension: the
    /// roots are the whole of level 0, so it drains one level and pulls nothing deeper.
    /// </summary>
    public static async ValueTask<IAsyncBreadthFirstTreenumerable<TNode>> SkipLastTreesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      int count,
      CancellationToken cancellationToken = default)
    {
      var treeCount = await source.CountTreesAsync(cancellationToken).ConfigureAwait(false);

      var takeCount = Math.Max(treeCount - count, 0);

      return source.TakeTrees(takeCount);
    }
  }
}
