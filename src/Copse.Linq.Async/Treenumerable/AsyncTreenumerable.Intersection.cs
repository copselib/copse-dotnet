using Copse.Core.Async;
using Copse.Linq.Treenumerators;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Intersection</c>: the merge pruned to the subtrees present on BOTH sides. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> Intersection<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> leftTreenumerable,
      IAsyncTreenumerable<TRight> rightTreenumerable)
    {
      return
        leftTreenumerable
        .Union(rightTreenumerable)
        .PruneBefore(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
    }

    public static IAsyncDepthFirstTreenumerable<MergeNode<TLeft, TRight>> Intersection<TLeft, TRight>(
      this IAsyncDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncDepthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .PruneBefore(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);

    public static IAsyncBreadthFirstTreenumerable<MergeNode<TLeft, TRight>> Intersection<TLeft, TRight>(
      this IAsyncBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => leftTreenumerable
        .Union(rightTreenumerable)
        .PruneBefore(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
  }
}
