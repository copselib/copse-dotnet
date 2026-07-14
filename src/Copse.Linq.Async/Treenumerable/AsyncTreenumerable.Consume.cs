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
    /// completes its one capture (pinning the depth-first layout if nothing has pulled yet);
    /// each node is fed exactly once, so side effects upstream of Memoize fire at most once per
    /// node, at whatever moment the capture reaches it -- for a single-moment capture of an
    /// impure source, Materialize. A completed buffer is already settled: a no-op (buffers are
    /// inert captures by contract -- see <see cref="IAsyncTreenumerableBuffer{TValue}"/>; a
    /// deferred capture stays deferred, its build pinned either way). Callers with a layout
    /// preference use the strategy overload: the strategy pins a FRESH buffer's layout.
    /// Awaitable -&gt; carries the <c>Async</c> suffix.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(TreeTraversalStrategy.DepthFirst).ConfigureAwait(false);
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
    /// Consume with a declared capture layout. On a lazy buffer the strategy is a SUGGESTION
    /// (a pin request): a fresh buffer's capture takes this layout; once anything has pulled,
    /// the existing capture completes and the argument is deliberately IGNORED -- the
    /// at-most-once invariant outranks it. Callers who need the layout GUARANTEED use
    /// Materialize(strategy). A completed buffer is a no-op. A plain tree is drained in the
    /// suggested dimension -- the one receiver where the suggestion is simply honored.
    /// </summary>
    public static async ValueTask ConsumeAsync<TNode>(
      this IAsyncTreenumerable<TNode> source,
      TreeTraversalStrategy suggestedStrategy,
      CancellationToken cancellationToken = default)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TNode> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(suggestedStrategy).ConfigureAwait(false);
        return;
      }

      if (source is IAsyncTreenumerableBuffer<TNode>)
        return;

      var treenumerator = source.GetAsyncTreenumerator(suggestedStrategy);
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
