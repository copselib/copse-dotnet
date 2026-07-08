using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of nodes in the (filtered) tree. Each node is scheduled exactly once, so
    /// this counts scheduling visits. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncTreenumerable<TNode> source)
      => source.CountNodesAsync(_ => true);

    public static async ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }

    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => source.CountNodesAsync(_ => true);

    public static async ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }

    public static ValueTask<int> CountNodesAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
      => source.CountNodesAsync(_ => true);

    public static async ValueTask<int> CountNodesAsync<TNode>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      if (source == null)
        return 0;

      var result = 0;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          if (predicate(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)))
            result++;

      return result;
    }
  }
}
