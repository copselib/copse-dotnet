using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The full depth-first visit stream (every scheduling/visiting visit), with a per-node
    /// strategy selector. Value flavor primary; the positional flavor is the arity-split (the
    /// Select/Where grammar, swept family-wide 2026-08-05).
    /// </summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node), cancellationToken);
    }

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node, nodeContext.Position), cancellationToken);
    }

    /// <summary>The full breadth-first visit stream, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node), cancellationToken);
    }

    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node, nodeContext.Position), cancellationToken);
    }

    /// <summary>The full depth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, cancellationToken);
    }

    /// <summary>The full breadth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, cancellationToken);
    }

    /// <summary>The full visit stream in the given dimension, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node), cancellationToken);
    }

    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), nodeContext => nodeTraversalStrategiesSelector(nodeContext.Node, nodeContext.Position), cancellationToken);
    }

    /// <summary>The full visit stream in the given dimension (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), cancellationToken);
    }

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = treenumeratorFactory();
      await using (treenumerator.ConfigureAwait(false))
      {
        if (!await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        yield return treenumerator.ToNodeVisit();

        var nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeContext());

        while (await treenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          yield return treenumerator.ToNodeVisit();

          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeContext());
          else
            nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;
        }
      }
    }

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = treenumeratorFactory();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          yield return treenumerator.ToNodeVisit();
        }
      }
    }
  }
}
