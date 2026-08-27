using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The full visit stream in the given dimension, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<TNode, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), nodeAndPosition => nodeTraversalStrategiesSelector(nodeAndPosition.Node), cancellationToken);
    }

    /// <summary>The positional flavor: the node's value and its position.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<TNode, NodePosition, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), nodeAndPosition => nodeTraversalStrategiesSelector(nodeAndPosition.Node, nodeAndPosition.Position), cancellationToken);
    }

    /// <summary>The full visit stream in the given dimension (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken = default)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), cancellationToken);
    }

    // The traversal core the whole Get*Traversal family drives through (the dimension-specific
    // files share these via the partial): pull under the consumer's per-node strategy, refresh
    // the strategy at each scheduling visit, TraverseAll between.
    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory,
      Func<NodeAndPosition<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var treenumerator = treenumeratorFactory();
      await using (treenumerator.ConfigureAwait(false))
      {
        if (!await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        yield return treenumerator.ToNodeVisit();

        var nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeAndPosition());

        while (await treenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          yield return treenumerator.ToNodeVisit();

          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeAndPosition());
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
