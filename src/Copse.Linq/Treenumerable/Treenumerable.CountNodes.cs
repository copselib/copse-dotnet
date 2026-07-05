using Copse.Core;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static int CountNodes<TNode>(this ITreenumerable<TNode> source)
      => source.CountNodes(_ => true);

    public static int CountNodes<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      if (source == null)
        return 0;

      var result = 0;

      using (var treenumerator = source.GetTreenumerator(treeTraversalStrategy))
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }

    public static int CountNodes<TNode>(this IDepthFirstTreenumerable<TNode> source)
      => source.CountNodes(_ => true);

    public static int CountNodes<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (source == null)
        return 0;

      var result = 0;

      using (var treenumerator = source.GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }

    public static int CountNodes<TNode>(this IBreadthFirstTreenumerable<TNode> source)
      => source.CountNodes(_ => true);

    public static int CountNodes<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (source == null)
        return 0;

      var result = 0;

      using (var treenumerator = source.GetBreadthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.SkipNode))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }
  }
}
