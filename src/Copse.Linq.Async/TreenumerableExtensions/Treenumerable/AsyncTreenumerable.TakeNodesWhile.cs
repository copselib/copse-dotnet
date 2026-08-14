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
    /// with an inverted predicate. Deferred. Value flavor primary; the positional flavor is the
    /// arity-split (the Select/Where grammar, swept family-wide 2026-08-05).
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(node => !predicate(node), keepFinalNode);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil((node, position) => !predicate(node, position), keepFinalNode);

    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(node => !predicate(node), keepFinalNode);

    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil((node, position) => !predicate(node, position), keepFinalNode);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil(node => !predicate(node), keepFinalNode);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesWhile<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => source.TakeNodesUntil((node, position) => !predicate(node, position), keepFinalNode);
  }
}
