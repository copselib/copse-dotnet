using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TNode> Where<TNode>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.Create(
          () => new WhereBreadthFirstTreenumerator<TNode>(
            source.GetBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode),
          () => new WhereDepthFirstTreenumerator<TNode>(
            source.GetDepthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }

    public static IDepthFirstTreenumerable<TNode> Where<TNode>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.CreateDepthFirst(
          () => new WhereDepthFirstTreenumerator<TNode>(
            source.GetDepthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }

    public static IBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.CreateBreadthFirst(
          () => new WhereBreadthFirstTreenumerator<TNode>(
            source.GetBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }
  }
}
