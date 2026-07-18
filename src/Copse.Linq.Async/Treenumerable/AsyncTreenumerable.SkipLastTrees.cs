using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Drops the last <paramref name="count"/> root trees. Awaits a root count first (the source is
    /// enumerated once to count, then again to take), so it is a ValueTask-returning terminal-builder.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerable<T>> SkipLastTreesAsync<T>(
      this IAsyncTreenumerable<T> source,
      int count,
      CancellationToken cancellationToken = default)
    {
      var treeCount = await source.CountTreesAsync(cancellationToken).ConfigureAwait(false);

      var takeCount = Math.Max(treeCount - count, 0);

      return source.TakeTrees(takeCount);
    }

    /// <summary>The depth-first-narrow twin: the same two passes (count the roots, then take), staying narrow.</summary>
    public static async ValueTask<IAsyncDepthFirstTreenumerable<T>> SkipLastTreesAsync<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
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
    public static async ValueTask<IAsyncBreadthFirstTreenumerable<T>> SkipLastTreesAsync<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      int count,
      CancellationToken cancellationToken = default)
    {
      var treeCount = await source.CountTreesAsync(cancellationToken).ConfigureAwait(false);

      var takeCount = Math.Max(treeCount - count, 0);

      return source.TakeTrees(takeCount);
    }
  }
}
