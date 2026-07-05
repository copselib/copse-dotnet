using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversal(source.GetDepthFirstTreenumerator, nodeTraversalStrategiesSelector);
    }

    public static IEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversal(source.GetBreadthFirstTreenumerator, nodeTraversalStrategiesSelector);
    }

    public static IEnumerable<NodeVisit<TNode>> GetDepthFirstTraversal<TNode>(
      this IDepthFirstTreenumerable<TNode> source)
    {
      return EnumerateTraversal(source.GetDepthFirstTreenumerator);
    }

    public static IEnumerable<NodeVisit<TNode>> GetBreadthFirstTraversal<TNode>(
      this IBreadthFirstTreenumerable<TNode> source)
    {
      return EnumerateTraversal(source.GetBreadthFirstTreenumerator);
    }

    public static IEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this ITreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      return EnumerateTraversal(() => source.GetTreenumerator(treeTraversalStrategy), nodeTraversalStrategiesSelector);
    }

    public static IEnumerable<NodeVisit<TNode>> GetTraversal<TNode>(
      this ITreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      return EnumerateTraversal(() => source.GetTreenumerator(treeTraversalStrategy));
    }

    private static IEnumerable<NodeVisit<TNode>> EnumerateTraversal<TNode>(
      Func<ITreenumerator<TNode>> treenumeratorFactory,
      Func<NodeContext<TNode>, NodeTraversalStrategies> nodeTraversalStrategiesSelector)
    {
      using (var treenumerator = treenumeratorFactory())
      {
        if (!treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          yield break;

        yield return treenumerator.ToNodeVisit();

        var nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeContext());

        while (treenumerator.MoveNext(nodeTraversalStrategies))
        {
          yield return treenumerator.ToNodeVisit();

          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            nodeTraversalStrategies = nodeTraversalStrategiesSelector(treenumerator.ToNodeContext());
          else
            nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;
        }
      }
    }

    private static IEnumerable<NodeVisit<TNode>> EnumerateTraversal<TNode>(
      Func<ITreenumerator<TNode>> treenumeratorFactory)
    {
      using (var treenumerator = treenumeratorFactory())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          yield return treenumerator.ToNodeVisit();
      }
    }
  }
}
