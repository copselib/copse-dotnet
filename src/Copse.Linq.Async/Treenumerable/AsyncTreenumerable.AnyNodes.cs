using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match.
    /// Drives with SkipNode where possible so each node is seen exactly once (at scheduling);
    /// the breadth-first dimension traverses all (its schedules front-run the skips).
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      var nodeTraversalStrategies =
        treeTraversalStrategy == TreeTraversalStrategy.BreadthFirst
        ? NodeTraversalStrategies.TraverseAll
        : NodeTraversalStrategies.SkipNode;

      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }

    public static async ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }

    public static async ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode && predicate(treenumerator.ToNodeContext()))
            return true;

      return false;
    }
  }
}
