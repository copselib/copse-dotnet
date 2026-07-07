using Copse.Core.Async;
using Copse.Linq.Treenumerators; // MergeNode

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>SymmetricDifference</c>: the merged nodes present on exactly one side. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> SymmetricDifference<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> left,
      IAsyncTreenumerable<TRight> right)
      => left.Union(right).Where(mergeNodeContext => !mergeNodeContext.Node.HasLeftAndRight);
  }
}
