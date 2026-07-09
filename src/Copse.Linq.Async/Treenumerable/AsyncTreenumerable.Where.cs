using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>Async <c>Where</c> (LINQ polarity: true = keep). Deferred; returns the filtered async tree.</summary>
    public static IAsyncTreenumerable<TNode> Where<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.Create(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode>(
            source.GetAsyncBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode),
          () => new AsyncWhereDepthFirstTreenumerator<TNode>(
            source.GetAsyncDepthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncDepthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateDepthFirst(
          () => new AsyncWhereDepthFirstTreenumerator<TNode>(
            source.GetAsyncDepthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }

    public static IAsyncBreadthFirstTreenumerable<TNode> Where<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return
        AsyncTreenumerableFactory.CreateBreadthFirst(
          () => new AsyncWhereBreadthFirstTreenumerator<TNode>(
            source.GetAsyncBreadthFirstTreenumerator,
            predicate,
            NodeTraversalStrategies.SkipNode));
    }
  }
}
