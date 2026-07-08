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
    {
      if (onNext == null)
        return source;

      return
        AsyncTreenumerableFactory
        .Create(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, onNext),
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, onNext));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Do<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, onNext));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Do<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, onNext));
    }
  }
}
