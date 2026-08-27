using Copse.Linq.Treenumerators;
using Copse;
using Copse.Treenumerables;
using Copse.Core;
using Copse.Linq;
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
        AsyncTree
        .Create(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, onNext),
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, onNext));
    }

    /// <summary>
    /// Async <c>Do</c>: runs a side effect on every emitted visit, forwarding the visit stream
    /// unchanged. Deferred (the effect runs during enumeration, once per <c>MoveNextAsync</c>).
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> Do<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        AsyncTree.CreateDepthFirst(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, onNext));
    }

    /// <summary>
    /// Async <c>Do</c>: runs a side effect on every emitted visit, forwarding the visit stream
    /// unchanged. Deferred (the effect runs during enumeration, once per <c>MoveNextAsync</c>).
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> Do<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Action<NodeVisit<TNode>> onNext)
    {
      if (onNext == null)
        return source;

      return
        AsyncTree.CreateBreadthFirst(
          () => new AsyncDoTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, onNext));
    }
  }
}
