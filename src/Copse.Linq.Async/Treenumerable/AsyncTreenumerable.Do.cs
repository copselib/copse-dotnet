using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>Do</c>: runs a side effect on every emitted visit, forwarding the visit stream
    /// unchanged. Deferred (the effect runs during enumeration, once per <c>MoveNextAsync</c>).
    /// </summary>
    public static IAsyncTreenumerable<TNode> Do<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncDoTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator(), onNext),
        () => new AsyncDoTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator(), onNext));
  }
}
