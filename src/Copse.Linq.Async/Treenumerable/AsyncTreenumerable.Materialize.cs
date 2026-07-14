using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Eagerly captures the source's current shape and returns the re-traversable capture:
    /// Memoize with the laziness removed. Awaitable -&gt; carries the <c>Async</c> suffix.
    /// Re-enumerating the result rides the in-memory capture -- cheap, still honoring dynamic
    /// NodeTraversalStrategies, never suspending in practice (the awaited grows complete
    /// synchronously over a finished capture) -- and never touches the source again.
    ///
    /// <para>Idempotent on a capture: a live memo is consumed in place (finishing whichever
    /// dimension is furthest along) and returned -- never wrapped; a completed
    /// <see cref="IAsyncTreenumerableBuffer{TValue}"/> is returned as-is (you already hold the
    /// ideal buffer -- re-capturing it would copy every node for nothing). The probe order
    /// matters: the lazy interface derives from the completed one, so it is tested first. A
    /// deferred capture (a buffer whose pinned build has not run yet) comes back still
    /// deferred -- the build is pinned either way, so Materialize adds nothing. Disposal of the
    /// returned buffer is vacuous once consumed (its feeds are already retired), so no
    /// <c>await using</c> is required.</para>
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncTreenumerable<TValue> source)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TValue> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync().ConfigureAwait(false);
        return lazyBuffer;
      }

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }

    /// <summary>
    /// Materialize with a GUARANTEED capture layout: the returned buffer's native-replay
    /// dimension is <paramref name="strategy"/>, whatever the input -- the argument is never
    /// ignored. A plain tree captures in that layout; a fresh memo pins it; a buffer already
    /// in that layout is returned as-is (a capture is never re-captured); a buffer in the
    /// OTHER layout is TRANSPOSED -- from the buffer, never from the source (buffer traversal
    /// is effect-free by contract, so at-most-once holds; the transpose is O(n) work, which
    /// this operator's name and return type already disclose -- and note a transposed result
    /// is a NEW instance). A partially-pinned memo completes its pinned capture first (the one
    /// source enumeration), then transposes. This is also the both-layouts recipe for
    /// speed-over-space callers: materialize once, then materialize THAT in the other
    /// dimension. Contrast Consume(strategy), where the strategy is only a suggestion --
    /// Materialize returns the buffer, so the layout IS the deliverable.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncTreenumerable<TValue> source, TreeTraversalStrategy strategy)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TValue> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(strategy).ConfigureAwait(false);
        return await WithNativeLayoutAsync(lazyBuffer, strategy).ConfigureAwait(false);
      }

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return await WithNativeLayoutAsync(completedBuffer, strategy).ConfigureAwait(false);

      var buffer = source.Memoize();
      await buffer.ConsumeAsync(strategy).ConfigureAwait(false);
      return buffer;
    }

    // The layout guarantee's back half: reuse a buffer that already complies (recognized via
    // the internal layout tag); otherwise TRANSPOSE from the buffer -- one cross-order walk of
    // the completed capture into the requested layout's arrays, the source untouched.
    // Implementations the library does not recognize transpose conservatively.
    private static async ValueTask<IAsyncTreenumerableBuffer<TValue>> WithNativeLayoutAsync<TValue>(
      IAsyncTreenumerableBuffer<TValue> buffer,
      TreeTraversalStrategy strategy)
    {
      if (buffer is IAsyncLayoutTaggedBuffer tagged && tagged.NativeLayout == strategy)
        return buffer;

      if (strategy == TreeTraversalStrategy.DepthFirst)
      {
        var preorderStore = await AsyncPreorderCapture.CaptureFromAsync(buffer).ConfigureAwait(false);

        return new AsyncCompletedTreenumerableBuffer<TValue>(
          new AsyncPreorderTreenumerable<TValue, AsyncPreorderArrayStore<TValue>>(preorderStore),
          TreeTraversalStrategy.DepthFirst);
      }

      var levelOrderStore = await AsyncLevelOrderCapture.CaptureFromAsync(buffer).ConfigureAwait(false);

      return new AsyncCompletedTreenumerableBuffer<TValue>(
        new AsyncLevelOrderTreenumerable<TValue, AsyncLevelOrderArrayStore<TValue>>(levelOrderStore),
        TreeTraversalStrategy.BreadthFirst);
    }

    /// <summary>
    /// Eager upgrades for single-dimension sources: capture the whole tree now, hand back the
    /// full citizen. The same buffer probes apply -- a narrow source that is secretly a capture
    /// is consumed in place or returned as-is, never re-captured.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncDepthFirstTreenumerable<TValue> source)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TValue> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync().ConfigureAwait(false);
        return lazyBuffer;
      }

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }

    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TValue> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync().ConfigureAwait(false);
        return lazyBuffer;
      }

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }
  }
}
