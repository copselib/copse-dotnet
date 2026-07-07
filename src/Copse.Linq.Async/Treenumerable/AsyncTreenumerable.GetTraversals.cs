using Copse.Core;
using Copse.Core.Async;
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
      => EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator, nodeTraversalStrategiesSelector);

    /// <summary>The full breadth-first visit stream, with a per-node strategy selector.</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
      => EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator, nodeTraversalStrategiesSelector);

    /// <summary>The full depth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => EnumerateTraversalAsync(source.GetAsyncDepthFirstTreenumerator);

    /// <summary>The full breadth-first visit stream (TraverseAll).</summary>
    public static IAsyncEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
      => EnumerateTraversalAsync(source.GetAsyncBreadthFirstTreenumerator);

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      var t = treenumeratorFactory();
      await using (t.ConfigureAwait(false))
      {
        if (!await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        yield return new NodeVisit<TNode>(t.Mode, t.Node, t.VisitCount, t.Position);

        var nodeTraversalStrategies = nodeTraversalStrategiesSelector(new NodeContext<TNode>(t.Node, t.Position));

        while (await t.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        {
          yield return new NodeVisit<TNode>(t.Mode, t.Node, t.VisitCount, t.Position);

          if (t.Mode == TreenumeratorMode.SchedulingNode)
            nodeTraversalStrategies = nodeTraversalStrategiesSelector(new NodeContext<TNode>(t.Node, t.Position));
        }
      }
    }

    private static async IAsyncEnumerable<NodeVisit<TNode>> EnumerateTraversalAsync<TNode>(
      Func<IAsyncTreenumerator<TNode>> treenumeratorFactory)
    {
      var t = treenumeratorFactory();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield return new NodeVisit<TNode>(t.Mode, t.Node, t.VisitCount, t.Position);
    }
  }
}
