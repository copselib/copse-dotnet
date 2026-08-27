using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Value flavor primary; the positional flavor is the arity split (the Select/Where
    /// grammar). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), treeTraversalStrategy, cancellationToken);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), treeTraversalStrategy, cancellationToken);

    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Value flavor primary; the positional flavor is the arity split (the Select/Where
    /// grammar). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), cancellationToken);

    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Value flavor primary; the positional flavor is the arity split (the Select/Where
    /// grammar). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), cancellationToken);

    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Value flavor primary; the positional flavor is the arity split (the Select/Where
    /// grammar). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), cancellationToken);

    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Value flavor primary; the positional flavor is the arity split (the Select/Where
    /// grammar). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => AnyNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), cancellationToken);

    private static async ValueTask<bool> AnyNodesCoreAsync<TNode>(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken)
    {
      var nodeTraversalStrategies =
        treeTraversalStrategy == TreeTraversalStrategy.BreadthFirst
        ? NodeTraversalStrategies.TraverseAll
        : NodeTraversalStrategies.SkipNode;

      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;
        }

      return false;
    }

    private static async ValueTask<bool> AnyNodesCoreAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      CancellationToken cancellationToken)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;
        }

      return false;
    }

    private static async ValueTask<bool> AnyNodesCoreAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      CancellationToken cancellationToken)
    {
      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;
        }

      return false;
    }
  }
}
