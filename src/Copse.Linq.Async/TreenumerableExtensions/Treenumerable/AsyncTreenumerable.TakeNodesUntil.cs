using Copse.Async;
using Copse.Async.Treenumerables;
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
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity-split (the Select/Where grammar, swept family-wide 2026-08-05).
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node), keepFinalNode);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), keepFinalNode);

    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node), keepFinalNode);

    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), keepFinalNode);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node), keepFinalNode);

    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), keepFinalNode);

    private static IAsyncTreenumerable<TNode> TakeNodesUntilCore<TNode>(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTree.Create(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator,
          predicate,
          keepFinalNode),
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          predicate,
          keepFinalNode));

    private static IAsyncDepthFirstTreenumerable<TNode> TakeNodesUntilCore<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          predicate,
          keepFinalNode));

    private static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntilCore<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTree.CreateBreadthFirst(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator,
          predicate,
          keepFinalNode));
  }
}
