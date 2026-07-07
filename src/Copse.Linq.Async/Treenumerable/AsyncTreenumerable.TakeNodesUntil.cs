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
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(source.GetAsyncBreadthFirstTreenumerator, predicate, keepFinalNode),
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(source.GetAsyncDepthFirstTreenumerator, predicate, keepFinalNode));
  }
}
