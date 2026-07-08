using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Treenumerators;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Union</c>: the structural merge of two trees by position -- each merged node carries
    /// whichever side(s) have a node at that position. The engine behind the other set ops. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> leftTreenumerable,
      IAsyncTreenumerable<TRight> rightTreenumerable)
      => AsyncTreenumerableFactory.Create(
        () => new AsyncStructuralMergeBreadthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetAsyncBreadthFirstTreenumerator,
          rightTreenumerable.GetAsyncBreadthFirstTreenumerator),
        () => new AsyncStructuralMergeDepthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetAsyncDepthFirstTreenumerator,
          rightTreenumerable.GetAsyncDepthFirstTreenumerator));

    public static IAsyncDepthFirstTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IAsyncDepthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncDepthFirstTreenumerable<TRight> rightTreenumerable)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncStructuralMergeDepthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetAsyncDepthFirstTreenumerator,
          rightTreenumerable.GetAsyncDepthFirstTreenumerator));

    public static IAsyncBreadthFirstTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IAsyncBreadthFirstTreenumerable<TLeft> leftTreenumerable,
      IAsyncBreadthFirstTreenumerable<TRight> rightTreenumerable)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncStructuralMergeBreadthFirstTreenumerator<TLeft, TRight>(
          leftTreenumerable.GetAsyncBreadthFirstTreenumerator,
          rightTreenumerable.GetAsyncBreadthFirstTreenumerator));
  }
}
