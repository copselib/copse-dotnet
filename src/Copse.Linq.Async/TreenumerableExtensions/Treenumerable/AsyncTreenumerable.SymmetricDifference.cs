using Copse.Core.Async;
using Copse.Linq.Treenumerators;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>SymmetricDifference</c>: the merged nodes present on exactly one side. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> leftTreenumerable,
      IAsyncTreenumerable<TRight> rightTreenumerable)
    {
      return
        leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasLeftAndRight);
    }

    public static IAsyncDepthFirstTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IAsyncDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncDepthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasLeftAndRight);

    public static IAsyncBreadthFirstTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IAsyncBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .Where(mergeNode => !mergeNode.HasLeftAndRight);
  }
}
