using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the node values of the (filtered) tree, in depth-first schedule order (each node
    /// once). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<List<TNode>> ToListAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var list = new List<TNode>();
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          if (t.Mode == TreenumeratorMode.SchedulingNode)
            list.Add(t.Node);
      return list;
    }
  }
}
