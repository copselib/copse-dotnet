using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<T> PruneBefore<T>(
      this ITreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.Create(
          () => new WhereBreadthFirstTreenumerator<T>(
            source.GetBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNodeAndDescendants),
          () => new WhereDepthFirstTreenumerator<T>(
            source.GetDepthFirstTreenumerator,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }

    public static IDepthFirstTreenumerable<T> PruneBefore<T>(
      this IDepthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.CreateDepthFirst(
          () => new WhereDepthFirstTreenumerator<T>(
            source.GetDepthFirstTreenumerator,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }

    public static IBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IBreadthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        TreenumerableFactory.CreateBreadthFirst(
          () => new WhereBreadthFirstTreenumerator<T>(
            source.GetBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }
  }
}
