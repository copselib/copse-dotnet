using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Drops the last <paramref name="count"/> root trees. Awaits a root count first (the source is
    /// enumerated once to count, then again to take), so it is a ValueTask-returning terminal-builder.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerable<T>> SkipLastTreesAsync<T>(this IAsyncTreenumerable<T> source, int count)
    {
      var treeCount = await source.CountTreesAsync().ConfigureAwait(false);
      var takeCount = Math.Max(treeCount - count, 0);
      return source.TakeTrees(takeCount);
    }
  }
}
