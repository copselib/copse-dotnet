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
    /// synchronously over a finished capture) -- and never touches the source again. On a tree
    /// that is already a buffer this consumes it in place (finishing whichever dimension is
    /// furthest along) rather than re-capturing; disposal of the returned buffer is vacuous once
    /// consumed (its feeds are already retired), so no <c>await using</c> is required.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncTreenumerable<TValue> source)
    {
      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }

    /// <summary>
    /// Materialize with a declared capture layout: the dimension named is the one captured, and
    /// therefore the one whose replays are native (the other dimension rides the same capture
    /// cross-order). Declared intent outranks sunk partial work in the other dimension; only an
    /// already-complete capture outranks the argument (a retired source is never re-enumerated).
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncTreenumerable<TValue> source, TreeTraversalStrategy strategy)
    {
      var buffer = source.Memoize();
      await buffer.ConsumeAsync(strategy).ConfigureAwait(false);
      return buffer;
    }

    /// <summary>
    /// Eager upgrades for single-dimension sources: capture the whole tree now, hand back the
    /// full citizen.
    /// </summary>
    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncDepthFirstTreenumerable<TValue> source)
    {
      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }

    public static async ValueTask<IAsyncTreenumerableBuffer<TValue>> MaterializeAsync<TValue>(this IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      var buffer = source.Memoize();
      await buffer.ConsumeAsync().ConfigureAwait(false);
      return buffer;
    }
  }
}
