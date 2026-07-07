using Copse.Async;
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
      => new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator()),
        () => new AsyncHideTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator()));
  }
}
