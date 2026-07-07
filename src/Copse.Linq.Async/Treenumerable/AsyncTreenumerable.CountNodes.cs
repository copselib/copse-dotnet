using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of nodes in the (filtered) tree. Each node is scheduled exactly once, so
    /// this counts scheduling visits. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<int> CountNodesAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var count = 0;
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            count++;
      return count;
    }
  }
}
