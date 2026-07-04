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
    // Idempotent: memoizing a memo returns it unchanged -- wrapping would chain a second capture
    // whose feed is the first's replay, copying every node twice for nothing. The instance
    // (hence its disposal) is therefore shared with the original holder, as any memo is.
    public static ITreenumerableBuffer<TValue> Memoize<TValue>(this ITreenumerable<TValue> source)
      => source as ITreenumerableBuffer<TValue> ?? new MemoizeTreenumerable<TValue>(source);
  }
}
