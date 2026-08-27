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
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity split (the Select/Where grammar).
    /// </summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node), keepFinalNode);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position), keepFinalNode);

    /// <summary>
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity split (the Select/Where grammar).
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node), keepFinalNode);

    /// <summary>
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity split (the Select/Where grammar).
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position), keepFinalNode);

    /// <summary>
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity split (the Select/Where grammar).
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node), keepFinalNode);

    /// <summary>
    /// Async <c>TakeNodesUntil</c>: forwards nodes until one matches the predicate, then stops
    /// scheduling (pruning that node's subtree and later siblings), keeping the matched node itself
    /// iff <paramref name="keepFinalNode"/>. Deferred. Value flavor primary; the positional
    /// flavor is the arity split (the Select/Where grammar).
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntil<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      bool keepFinalNode)
      => TakeNodesUntilCore(source, nodeAndPosition => predicate(nodeAndPosition.Node, nodeAndPosition.Position), keepFinalNode);

    private static IAsyncTreenumerable<TNode> TakeNodesUntilCore<TNode>(
      IAsyncTreenumerable<TNode> source,
      Func<NodeAndPosition<TNode>, bool> predicate,
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
      Func<NodeAndPosition<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator,
          predicate,
          keepFinalNode));

    private static IAsyncBreadthFirstTreenumerable<TNode> TakeNodesUntilCore<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeAndPosition<TNode>, bool> predicate,
      bool keepFinalNode)
      => AsyncTree.CreateBreadthFirst(
        () => new AsyncTakeNodesUntilTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator,
          predicate,
          keepFinalNode));
  }
}
