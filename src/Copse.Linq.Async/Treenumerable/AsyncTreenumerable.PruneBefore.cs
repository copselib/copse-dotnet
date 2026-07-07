using Copse.Async;
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
    /// predicate -- no child promotion (SkipNodeAndDescendants). "Prune when true", so the removal
    /// polarity inverts here at the operator, over the Where machinery (keep when true). Deferred.
    /// </summary>
    public static IAsyncTreenumerable<TNode> PruneBefore<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (predicate == null)
        return source;

      return new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncWhereBreadthFirstTreenumerator<TNode>(
          source.GetAsyncBreadthFirstTreenumerator, nodeContext => !predicate(nodeContext), NodeTraversalStrategies.SkipNodeAndDescendants),
        () => new AsyncWhereDepthFirstTreenumerator<TNode>(
          source.GetAsyncDepthFirstTreenumerator, nodeContext => !predicate(nodeContext), NodeTraversalStrategies.SkipNodeAndDescendants));
    }
  }
}
