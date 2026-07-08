using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>TakeNodesWhile</c>: forwards nodes while they match the predicate -- TakeNodesUntil
    /// with an inverted predicate. Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTreenumerableFactory.Create(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator,
          nodeVisit => !predicate(nodeVisit),
          keepFinalNode),
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          nodeVisit => !predicate(nodeVisit),
          keepFinalNode));

    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(nodeVisit => !predicate(nodeVisit), keepFinalNode);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(nodeVisit => !predicate(nodeVisit), keepFinalNode);
  }
}
