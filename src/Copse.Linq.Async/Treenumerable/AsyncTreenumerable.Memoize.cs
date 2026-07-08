using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Turns a tree into a re-traversable, shared, lazily-growing capture of its current shape:
    /// each enumeration replays from the capture instead of re-running the source, paying to
    /// build only the region it actually reaches, in whichever traversal dimension it uses.
    /// Deferred (nothing is pulled until a replay or <c>ConsumeAsync</c> demands it), so it keeps
    /// the sync name. See MEMOIZE_DESIGN.md for the full contract (per-dimension buffers, the
    /// four-case serving rule, disposal semantics).
    ///
    /// <para>Idempotent on a live memo: memoizing an <see cref="IAsyncLazyTreenumerableBuffer{TValue}"/>
    /// returns it unchanged -- wrapping would chain a second capture whose feed is the first's
    /// replay, copying every node twice for nothing. The instance (hence its disposal) is
    /// therefore shared with the original holder, as any memo is. A completed capture (a non-lazy
    /// <see cref="IAsyncTreenumerableBuffer{TValue}"/> -- what Materialize returns) is NOT
    /// short-circuited: it has no live feed to share, and re-memoizing it (a degenerate call --
    /// you already hold the ideal buffer) simply wraps it.</para>
    /// </summary>
    public static IAsyncLazyTreenumerableBuffer<TValue> Memoize<TValue>(this IAsyncTreenumerable<TValue> source)
      => source as IAsyncLazyTreenumerableBuffer<TValue> ?? new AsyncMemoizeTreenumerable<TValue>(source);

    /// <summary>
    /// The typed upgrade op on a depth-first-only source: the returned buffer is a full
    /// treenumerable again -- the memo's single preorder capture serves depth-first replays
    /// natively and breadth-first replays cross-order, so O(n) space is what buys back the
    /// missing dimension (see TRAVERSAL_DIMENSION_SPLIT.md). A source that is secretly a full
    /// citizen routes to the richer two-buffer memo (cheaper when rich), which also preserves
    /// buffer idempotency.
    /// </summary>
    public static IAsyncLazyTreenumerableBuffer<TValue> Memoize<TValue>(this IAsyncDepthFirstTreenumerable<TValue> source)
      => source is IAsyncTreenumerable<TValue> fullCitizen
        ? fullCitizen.Memoize()
        : new AsyncMemoizeDepthFirstSourceTreenumerable<TValue>(source);

    /// <summary>
    /// The dual upgrade on a breadth-first-only source. Notably the ONLY road to such a
    /// source's depth-first dimension (no bounded streaming strategy exists for that
    /// direction) -- the escalation the split makes explicit.
    /// </summary>
    public static IAsyncLazyTreenumerableBuffer<TValue> Memoize<TValue>(this IAsyncBreadthFirstTreenumerable<TValue> source)
      => source is IAsyncTreenumerable<TValue> fullCitizen
        ? fullCitizen.Memoize()
        : new AsyncMemoizeBreadthFirstSourceTreenumerable<TValue>(source);
  }
}
