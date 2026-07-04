using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static bool AnyNodes<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      var nodeTraversalStrategies =
        treeTraversalStrategy == TreeTraversalStrategy.BreadthFirst
        ? NodeTraversalStrategies.TraverseAll
        : NodeTraversalStrategies.SkipNode;

      using (var treenumerator = source.GetTreenumerator(treeTraversalStrategy))
        while (treenumerator.MoveNext(nodeTraversalStrategies))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }

    public static bool AnyNodes<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      using (var treenumerator = source.GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }

    public static bool AnyNodes<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      using (var treenumerator = source.GetBreadthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }
  }
}
