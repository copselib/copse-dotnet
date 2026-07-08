using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The full depth-first visit stream (every scheduling/visiting visit), with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeTraversalStrategiesSelector);
    }

    /// <summary>The full breadth-first visit stream, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeTraversalStrategiesSelector);
    }

    /// <summary>The full depth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      return EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator);
    }

    /// <summary>The full breadth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      return EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator);
    }

    /// <summary>The full visit stream in the given dimension, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy), nodeTraversalStrategiesSelector);
    }

    /// <summary>The full visit stream in the given dimension (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      return EnumerateTraversalAsync(() => source.GetAsyncTreenumerator(treeTraversalStrategy));
    }

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
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
          yield return treenumerator.ToNodeVisit();

          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeContext());
          else
            nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;
        }
      }
    }

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory)
    {
      var treenumerator = treenumeratorFactory();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield return treenumerator.ToNodeVisit();
      }
    }
  }
}
