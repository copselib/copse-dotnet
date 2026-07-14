using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Terminal: fully settle the tree with the minimum work. A plain tree is driven to
    /// exhaustion (depth-first) for its side effects, discarding the visit stream. A lazy buffer
    /// finishes whichever dimension's capture is furthest along -- both count toward the same
    /// total, so the larger buffered count is the cheaper capture to complete, depth-first
    /// winning ties (and hence the fresh, nothing-buffered case); each node is fed exactly once,
    /// so side effects upstream of Memoize fire at most once per node, at whatever moment the
    /// capture reaches it -- for a single-moment capture of an impure source, Materialize. A
    /// completed buffer is already settled: a no-op (buffers are inert captures by contract --
    /// see <see cref="IAsyncTreenumerableBuffer{TValue}"/>; a deferred capture stays deferred,
    /// its build pinned either way). Callers with a layout preference use the strategy overload:
    /// declared intent outranks sunk cost. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(
          lazyBuffer.GetBufferedCount(TreeTraversalStrategy.DepthFirst) >= lazyBuffer.GetBufferedCount(TreeTraversalStrategy.BreadthFirst)
            ? TreeTraversalStrategy.DepthFirst
            : TreeTraversalStrategy.BreadthFirst).ConfigureAwait(false);
        return;
      }

      if (source is IAsyncTreenumerableBuffer<TNode>)
        return;

      var treenumerator = source.GetAsyncTreenumerator(TreeTraversalStrategy.DepthFirst);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Consume with a declared capture layout. A lazy buffer completes THAT dimension's capture
    /// -- declared intent outranks sunk partial work in the other dimension (the Materialize
    /// precedent), so this may enumerate the source again through the named dimension's feed. A
    /// completed buffer is a no-op. A plain tree is drained in the named dimension.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy treeTraversalStrategy,
      CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(treeTraversalStrategy).ConfigureAwait(false);
        return;
      }

      if (source is IAsyncTreenumerableBuffer<TNode>)
        return;

      var treenumerator = source.GetAsyncTreenumerator(treeTraversalStrategy);
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// The depth-first-narrow entry, with the same buffer probes: in the caller's declared
    /// depth-first world, a lazy buffer completes its depth-first capture, a completed buffer
    /// no-ops, and a plain narrow source is drained.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(TreeTraversalStrategy.DepthFirst).ConfigureAwait(false);
        return;
      }

      if (source is IAsyncTreenumerableBuffer<TNode>)
        return;

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>The breadth-first-narrow twin of the entry above.</summary>
    public static async ValueTask ConsumeAsync<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source, CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(TreeTraversalStrategy.BreadthFirst).ConfigureAwait(false);
        return;
      }

      if (source is IAsyncTreenumerableBuffer<TNode>)
        return;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
        }
    }
  }
}
