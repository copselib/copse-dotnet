using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of nodes in the (filtered) tree. Each node is scheduled exactly once, so
    /// this counts scheduling visits. Value flavor primary; the positional flavor is the
    /// arity-split (the Select/Where grammar, swept family-wide 2026-08-05).
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncTreenumerable<TNode> source, CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, _ => true, default, cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), treeTraversalStrategy, cancellationToken);

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), treeTraversalStrategy, cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, _ => true, cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, _ => true, cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, bool> predicate,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node), cancellationToken);

    public static ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, bool> predicate,
      CancellationToken cancellationToken = default)
      => CountNodesCoreAsync(source, nodeContext => predicate(nodeContext.Node, nodeContext.Position), cancellationToken);

    private static async ValueTask<int> CountNodesCoreAsync<TNode>(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;
        }

      return result;
    }

    private static async ValueTask<int> CountNodesCoreAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      CancellationToken cancellationToken)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;
        }

      return result;
    }

    private static async ValueTask<int> CountNodesCoreAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      CancellationToken cancellationToken)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;
        }

      return result;
    }
  }
}
