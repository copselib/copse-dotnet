using Copse.Core;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Eagerly captures the source's current shape and returns the re-traversable capture:
    // Memoize with the laziness removed. Re-enumerating the result rides the in-memory capture
    // -- cheap, still honoring dynamic NodeTraversalStrategies -- and never touches the source
    // again. On a tree that is already a buffer this consumes it in place (finishing whichever
    // dimension is furthest along) rather than re-capturing; disposal of the returned buffer is
    // vacuous once consumed (its feeds are already retired), so no using is required.
    public static ITreenumerableBuffer<TValue> Materialize<TValue>(this ITreenumerable<TValue> source)
    {
      var buffer = source.Memoize();
      buffer.Consume();
      return buffer;
    }

    // Materialize with a declared capture layout: the dimension named is the one captured, and
    // therefore the one whose replays are native (the other dimension rides the same capture
    // cross-order). Declared intent outranks sunk partial work in the other dimension; only an
    // already-complete capture outranks the argument (a retired source is never re-enumerated).
    public static ITreenumerableBuffer<TValue> Materialize<TValue>(this ITreenumerable<TValue> source, TreeTraversalStrategy strategy)
    {
      var buffer = source.Memoize();
      buffer.Consume(strategy);
      return buffer;
    }
  }
}
