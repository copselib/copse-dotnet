using Copse.Core;
using Copse.Core.Async;
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
      => source.TakeNodesUntil(nodeContext => !predicate(nodeContext), keepFinalNode);
  }
}
