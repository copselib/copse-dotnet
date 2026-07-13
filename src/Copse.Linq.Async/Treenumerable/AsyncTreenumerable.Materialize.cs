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
    /// Materialize with a declared capture layout: the dimension named is the one captured, and
    /// therefore the one whose replays are native (the other dimension rides the same capture
    /// cross-order). Declared intent outranks sunk partial work in the other dimension; only an
    /// already-complete capture outranks the argument (a retired source is never re-enumerated,
    /// and a completed buffer is returned as-is -- its layout is already fixed).
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncTreenumerable<TValue> source, TreeTraversalStrategy strategy)
    {
      if (source is IAsyncLazyTreenumerableBuffer<TValue> lazyBuffer)
      {
        await lazyBuffer.ConsumeAsync(strategy).ConfigureAwait(false);
        return lazyBuffer;
      }

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      var buffer = source.Memoize();
      await buffer.ConsumeAsync(strategy).ConfigureAwait(false);
      return buffer;
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
