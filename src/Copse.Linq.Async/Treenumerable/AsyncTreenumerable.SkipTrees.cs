using Copse.Core;
using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Drops the first <paramref name="count"/> root trees (prunes them before their first visit). Deferred.</summary>
    public static IAsyncTreenumerable<T> SkipTrees<T>(
      this IAsyncTreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);

    public static IAsyncDepthFirstTreenumerable<T> SkipTrees<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);

    public static IAsyncBreadthFirstTreenumerable<T> SkipTrees<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      int count)
      => source.PruneBefore(step => step.Position.Depth == 0 && step.Position.SiblingIndex < count);
  }
}
