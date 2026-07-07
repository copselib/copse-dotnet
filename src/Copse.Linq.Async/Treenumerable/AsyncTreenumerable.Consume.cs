using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: drives the tree to exhaustion for its side effects, discarding the visit stream.
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
        }
    }
  }
}
