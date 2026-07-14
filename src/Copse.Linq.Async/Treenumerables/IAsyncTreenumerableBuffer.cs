using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // An owned, in-memory, re-traversable capture of a tree: a full treenumerable (both
  // dimensions available, random access) whose backing storage it owns. This is the
  // "materialized" disclosure marker that eager capture operators (Materialize, LeaffixScan,
  // Invert) return -- the O(n) is already paid and the result is self-contained.
  //
  // Deliberately NOT disposable: a completed capture holds only managed arrays, with no live
  // source feed to retire, so there is nothing to dispose and it chains freely through the
  // fluent surface. The still-growing case -- which DOES hold a live feed -- is
  // the lazy buffer below.
  //
  // CONTRACT -- a buffer is a capture, not a computation: traversing it is effect-free and
  // idempotent. A deferred capture may run its pinned build on first use, but that build runs
  // at most once and is itself a capture (anything effectful lives upstream of it and fires at
  // most once, at capture time). The library optimizes on this everywhere -- Materialize
  // returns a buffer as-is, Consume no-ops on one -- so an implementation whose traversal has
  // observable effects is out of contract, not merely exotic.
  public interface IAsyncTreenumerableBuffer<TValue> : IAsyncTreenumerable<TValue>
  {
  }

  // A buffer still backed by a LIVE source feed: the lazily-growing capture Memoize returns.
  // It holds inner treenumerators paused mid-traversal over the source (the captured data
  // itself is just managed memory), so it is disposable -- disposing stops all future source
  // consumption: enumerators already replaying keep working over the captured region, but one
  // that needs data beyond it throws ObjectDisposedException.
  //
  // Because it IS a treenumerable buffer it composes anywhere a capture is expected; but the
  // fluent surface sees only the non-disposable base, so the caller keeps this reference to
  // dispose it (a chain typed as the base will not).
  public interface IAsyncLazyTreenumerableBuffer<TValue> : IAsyncTreenumerableBuffer<TValue>, IAsyncDisposable
  {
    // True once either dimension's capture is complete: the whole tree is held and the source
    // is permanently retired -- no future enumeration, in either dimension, touches it again.
    bool IsComplete { get; }

    // Nodes captured so far in the given dimension's buffer (0 if that dimension has never been
    // enumerated). Both dimensions count toward the same total, so the larger count is the
    // cheaper capture to finish -- the comparison Consume()/Materialize() policy runs on. Not a
    // progress fraction: the tree's size is unknown until a capture completes.
    int GetBufferedCount(TreeTraversalStrategy strategy);

    // Drive the given dimension's capture to completion (MoreLINQ's Consume, aimed at one
    // dimension). Obedient below the invariant: a no-op iff IsComplete -- a retired source is
    // never re-enumerated, no argument overrides the memo's single-shape guarantee. See the
    // parameterless Consume() extension for the count-comparison policy.
    ValueTask ConsumeAsync(TreeTraversalStrategy strategy);
  }
}
