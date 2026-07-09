using Copse.Core.Async;
using Copse.Linq.Async;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Hide</c>: forwards the visit stream unchanged behind the plain
    /// <see cref="IAsyncTreenumerable{TNode}"/> contract, so callers can't downcast to (or feature-test
    /// for) the concrete source type. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> Hide<TNode>(
      this IAsyncTreenumerable<TNode> source)
    {
      return new AsyncHideTreenumerable<TNode>(source);
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator));

    public static IAsyncBreadthFirstTreenumerable<TNode> Hide<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator));
  }
}
