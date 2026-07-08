using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of trees in the forest (root nodes) -- the count of <c>GetRoots</c>.
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<int> CountTreesAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var count = 0;

      await foreach (var root in source.GetRoots().ConfigureAwait(false))
        count++;

      return count;
    }
  }
}
