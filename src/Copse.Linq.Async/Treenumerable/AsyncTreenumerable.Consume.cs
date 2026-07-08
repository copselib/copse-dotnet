using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: drives the tree to exhaustion for its side effects, discarding the visit stream.
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy = default)
    {
      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false)) ;
    }

    /// <summary>
    /// Drive a buffer's capture to completion without naming a dimension: finish whichever
    /// dimension's capture is furthest along -- both count toward the same total, so the larger
    /// buffered count is the cheaper capture to complete -- with depth-first winning ties (and
    /// hence the fresh, nothing-buffered case). A no-op on an already-complete buffer, via the
    /// member's invariant. Callers with a layout preference use ConsumeAsync(TreeTraversalStrategy)
    /// directly: declared intent outranks sunk cost. (Overload resolution keeps this and the
    /// drain above apart: the buffer receiver is more specific, and the strategy-taking member
    /// on IAsyncLazyTreenumerableBuffer beats any extension.) Growth control lives on the lazy
    /// buffer only -- a completed capture has nothing left to consume.
    /// </summary>
    public static ValueTask ConsumeAsync<TValue>(this IAsyncLazyTreenumerableBuffer<TValue> buffer)
      => buffer.ConsumeAsync(
        buffer.GetBufferedCount(TreeTraversalStrategy.DepthFirst) >= buffer.GetBufferedCount(TreeTraversalStrategy.BreadthFirst)
          ? TreeTraversalStrategy.DepthFirst
          : TreeTraversalStrategy.BreadthFirst);

    public static async ValueTask ConsumeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false)) ;
    }

    public static async ValueTask ConsumeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false)) ;
    }
  }
}
