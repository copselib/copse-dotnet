using Copse.Core.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Subtract</c>: the left tree minus the nodes also present on the right, projected back
    /// to the left values. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> leftTreenumerable,
      IAsyncTreenumerable<TRight> rightTreenumerable)
    {
      return
        leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasRight)
        .Select(mergeNode => mergeNode.Left);
    }

    public static IAsyncDepthFirstTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IAsyncDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncDepthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasRight)
        .Select(mergeNode => mergeNode.Left);

    public static IAsyncBreadthFirstTreenumerable<TLeft> Subtract<TLeft, TRight>(
      this IAsyncBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasRight)
        .Select(mergeNode => mergeNode.Left);
  }
}
