using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: whether any node satisfies the predicate. Short-circuits on the first match. Drives
    /// with SkipNode so each node is seen exactly once (at scheduling). Awaitable -&gt; <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<bool> AnyNodesAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.SkipNode).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode && predicate(new NodeContext<TNode>(t.Node, t.Position)))
            return true;
      return false;
    }
  }
}
