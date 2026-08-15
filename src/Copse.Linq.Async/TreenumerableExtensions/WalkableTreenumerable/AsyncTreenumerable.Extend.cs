using Copse.Async;
using Copse.Linq.Async.Treenumerables;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The comonad's co-bind (docs/CATEGORY_THEORY_SURVEY.md §6): relabel every node by an
    /// arbitrary OBSERVATION of its focus. The observer receives the walkable and the handle,
    /// so it can consult anything reachable from that vantage -- depth, ancestor values,
    /// subtree facts -- which is exactly what streaming <c>Select</c> cannot see. The shape
    /// and the handles are untouched (extend relabels, never reshapes); the result is a
    /// walkable whose streaming half is the Walk adapter driving the source's adjacency under
    /// the observer's labeling. The scans are this operation restricted to observations that
    /// factor through a fold along the traversal order -- pinned by the scan-coherence laws
    /// in the walker comonad suites.
    ///
    /// <para>Laws (the Store comonad's, pinned): extend of extract is the identity; extract
    /// after extend recovers the observer; and extend co-associates.</para>
    /// </summary>
    public static IAsyncWalkableTreenumerable<TResult, THandle> Extend<TValue, THandle, TResult>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source,
      Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> observer)
      // Stage C: the walkable no longer exposes its topology, so the relabeling binds "the
      // topology this walkable's door will hand over" -- deferred, knocked once at the
      // first pull or probe. The empty forest needs no special case: the door topology
      // misses honestly everywhere.
      => new AsyncExtendWalkable<TValue, THandle, TResult>(new AsyncDoorTopology<TValue, THandle>(source), observer);

    // The topology-receiver form: the algebra at SPI altitude, for machinery that already
    // holds a topology (the lens compositions, the clamp).
    internal static IAsyncWalkableTreenumerable<TResult, THandle> Extend<TValue, THandle, TResult>(
      this IAsyncTreeTopology<TValue, THandle> source,
      Func<IAsyncTreeTopology<TValue, THandle>, THandle, ValueTask<TResult>> observer)
      => new AsyncExtendWalkable<TValue, THandle, TResult>(source, observer);
  }
}
