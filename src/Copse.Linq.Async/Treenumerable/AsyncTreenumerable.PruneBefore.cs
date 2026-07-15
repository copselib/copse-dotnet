using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>PruneBefore</c>: prunes each subtree at (and including) the first node matching the
    /// predicate -- no child promotion (SkipNodeAndDescendants). Deferred.
    /// </summary>
    public static IAsyncTreenumerable<T> PruneBefore<T>(
      this IAsyncTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.Create(
          // PruneBefore's predicate means "prune when true"; the Where machinery keeps when
          // true (the LINQ convention), so removal semantics invert here, at the operator.
          () => new AsyncWhereBreadthFirstTreenumerator<T, T>(
            source.GetAsyncBreadthFirstTreenumerator,
            AsyncIdentitySelector<T>.Instance,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants),
          () => new AsyncWhereDepthFirstTreenumerator<T, T>(
            source.GetAsyncDepthFirstTreenumerator,
            AsyncIdentitySelector<T>.Instance,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }

    public static IAsyncDepthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncDepthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<T, T>(
            source.GetAsyncDepthFirstTreenumerator,
            AsyncIdentitySelector<T>.Instance,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }

    public static IAsyncBreadthFirstTreenumerable<T> PruneBefore<T>(
      this IAsyncBreadthFirstTreenumerable<T> source,
      Func<NodeContext<T>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<T, T>(
            source.GetAsyncBreadthFirstTreenumerator,
            AsyncIdentitySelector<T>.Instance,
            nodeContext => !predicate(nodeContext),
            NodeTraversalStrategies.SkipNodeAndDescendants));
    }
  }
}
