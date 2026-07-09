using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the node values of the (filtered) tree, in depth-first schedule order (each node
    /// once). Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<List<TNode>> ToListAsync<TNode>(this IAsyncTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      var list = new List<TNode>();
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            list.Add(treenumerator.Node);
        }
      return list;
    }
  }
}
