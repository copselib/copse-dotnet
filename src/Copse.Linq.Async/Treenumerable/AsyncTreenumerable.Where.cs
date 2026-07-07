using Copse.Async;
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

      return new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncWhereBreadthFirstTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator, predicate, NodeTraversalStrategies.SkipNode),
        () => new AsyncWhereDepthFirstTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator, predicate, NodeTraversalStrategies.SkipNode));
    }
  }
}
