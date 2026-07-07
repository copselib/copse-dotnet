using Copse.Core.Async;
using Copse.Linq.Treenumerators; // MergeNode

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Intersection</c>: the merge pruned to the subtrees present on BOTH sides. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> Intersection<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> left,
      IAsyncTreenumerable<TRight> right)
      => left.Union(right).PruneBefore(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
  }
}
