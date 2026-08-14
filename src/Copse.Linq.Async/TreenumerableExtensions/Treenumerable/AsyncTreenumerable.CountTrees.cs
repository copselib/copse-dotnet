using Copse.Core;
using Copse.Core.Async;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: the number of trees in the forest (root nodes) -- the count of <c>GetRoots</c>.
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask<int> CountTreesAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      var count = 0;

      await foreach (var root in source.GetRoots(cancellationToken).ConfigureAwait(false))
        count++;

      return count;
    }

    /// <summary>
    /// The breadth-first twin. The counting pass is nearly free in this dimension: the roots are
    /// the whole of level 0, so driving with SkipNodeAndDescendants schedules each root once and
    /// never pulls anything deeper.
    /// </summary>
    public static async ValueTask<int> CountTreesAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      var count = 0;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNodeAndDescendants).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          count++;
        }
      }

      return count;
    }

    /// <summary>Disambiguation overload for full trees; keeps the depth-first consumption.</summary>
    public static ValueTask<int> CountTreesAsync<TNode>(this IAsyncTreenumerable<TNode> source, CancellationToken cancellationToken = default)
      => CountTreesAsync((IAsyncDepthFirstTreenumerable<TNode>)source, cancellationToken);
  }
}
