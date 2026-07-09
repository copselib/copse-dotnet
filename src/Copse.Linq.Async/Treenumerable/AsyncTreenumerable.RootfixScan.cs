using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's value becomes the
    /// accumulator applied to its parent's accumulated value and the node (a prefix-fold down each
    /// root-to-node path). Transforms the <c>TNode</c> tree into a <c>TAccumulate</c> tree. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.Create(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          accumulator,
          seed),
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IAsyncDepthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IAsyncBreadthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          accumulator,
          seed));
  }
}
