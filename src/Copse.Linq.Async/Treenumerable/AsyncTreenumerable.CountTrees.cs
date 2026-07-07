using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of trees in the forest (root nodes). Drives with SkipNodeAndDescendants so
    /// each root is scheduled once and its subtree skipped. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<int> CountTreesAsync<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      var count = 0;
      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
        while (await t.MoveNextAsync(NodeTraversalStrategies.SkipNodeAndDescendants).ConfigureAwait(false))
          count++;
      return count;
    }
  }
}
