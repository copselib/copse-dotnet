using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Drops the first <paramref name="count"/> root trees (prunes them before their first visit).</summary>
    public static IAsyncTreenumerable<T> SkipTrees<T>(this IAsyncTreenumerable<T> source, int count)
      => source.PruneBefore(nc => nc.Position.Depth == 0 && nc.Position.SiblingIndex < count);
  }
}
