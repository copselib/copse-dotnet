using Copse.Core;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Turns a tree into a re-traversable, shared, lazily-growing capture of its current shape:
    // each enumeration replays from the capture instead of re-running the source, paying to
    // build only the region it actually reaches, in whichever traversal dimension it uses.
    // See MEMOIZE_DESIGN.md for the full contract (per-dimension buffers, the four-case serving
    // rule, disposal semantics).
    //
    // Idempotent on a live memo: memoizing an ILazyTreenumerableBuffer returns it unchanged --
    // wrapping would chain a second capture whose feed is the first's replay, copying every node
    // twice for nothing. The instance (hence its disposal) is therefore shared with the original
    // holder, as any memo is. A completed capture (a non-lazy ITreenumerableBuffer -- what
    // Materialize/LeaffixScan/Invert return) is NOT short-circuited: it has no live feed to share,
    // and re-memoizing it (a degenerate call -- you already hold the ideal buffer) simply wraps it.
    public static ILazyTreenumerableBuffer<TValue> Memoize<TValue>(this ITreenumerable<TValue> source)
      => source as ILazyTreenumerableBuffer<TValue> ?? new MemoizeTreenumerable<TValue>(source);

    // The typed upgrade op on a depth-first-only source: the returned buffer is a full
    // ITreenumerable again -- the memo's single preorder capture serves depth-first replays
    // natively and breadth-first replays cross-order, so O(n) space is what buys back the
    // missing dimension (see TRAVERSAL_DIMENSION_SPLIT.md). A source that is secretly a full
    // citizen routes to the richer two-buffer memo (cheaper when rich), which also preserves
    // buffer idempotency.
    public static ILazyTreenumerableBuffer<TValue> Memoize<TValue>(this IDepthFirstTreenumerable<TValue> source)
      => source is ITreenumerable<TValue> fullCitizen
        ? fullCitizen.Memoize()
        : new MemoizeDepthFirstSourceTreenumerable<TValue>(source);

    // The dual upgrade on a breadth-first-only source. Notably the ONLY road to such a
    // source's depth-first dimension (no bounded streaming strategy exists for that
    // direction) -- the escalation the split makes explicit.
    public static ILazyTreenumerableBuffer<TValue> Memoize<TValue>(this IBreadthFirstTreenumerable<TValue> source)
      => source is ITreenumerable<TValue> fullCitizen
        ? fullCitizen.Memoize()
        : new MemoizeBreadthFirstSourceTreenumerable<TValue>(source);
  }
}
