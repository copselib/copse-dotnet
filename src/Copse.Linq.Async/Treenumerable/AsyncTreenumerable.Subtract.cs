using Copse.Core.Async;
using Copse.Linq.Treenumerators; // MergeNode

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Subtract</c>: the left tree minus the nodes also present on the right, projected back
    /// to the left values. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> left,
      IAsyncTreenumerable<TRight> right)
      => left.Union(right)
        .Where(mergeNodeContext => !mergeNodeContext.Node.HasRight)
        .Select(mergeNodeContext => mergeNodeContext.Node.Left);
  }
}
