using Copse.Async;
using Copse.Core.Async;
using Copse.Linq.Async;
using Copse.Linq.Treenumerators; // MergeNode

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Union</c>: the structural merge of two trees by position -- each merged node carries
    /// whichever side(s) have a node at that position. The engine behind the other set ops. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<MergeNode<TLeft, TRight>> Union<TLeft, TRight>(
      this IAsyncTreenumerable<TLeft> left,
      IAsyncTreenumerable<TRight> right)
      => new AsyncDelegatingTreenumerable<MergeNode<TLeft, TRight>>(
        () => new AsyncStructuralMergeBreadthFirstTreenumerator<TLeft, TRight>(
          left.GetAsyncBreadthFirstTreenumerator, right.GetAsyncBreadthFirstTreenumerator),
        () => new AsyncStructuralMergeDepthFirstTreenumerator<TLeft, TRight>(
          left.GetAsyncDepthFirstTreenumerator, right.GetAsyncDepthFirstTreenumerator));
  }
}
