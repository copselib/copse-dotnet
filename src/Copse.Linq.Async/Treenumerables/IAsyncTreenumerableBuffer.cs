using Copse.Core.Async;

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
  // IAsyncLazyTreenumerableBuffer.
  //
  // CONTRACT -- a buffer is a capture, not a computation: traversing it is effect-free and
  // idempotent. A deferred capture may run its pinned build on first use, but that build runs
  // at most once and is itself a capture (anything effectful lives upstream of it and fires at
  // most once, at capture time). The library optimizes on this -- Materialize
  // returns a compliant buffer as-is instead of re-capturing -- so an implementation whose
  // traversal has observable effects is out of contract, not merely exotic.
  public interface IAsyncTreenumerableBuffer<TValue> : IAsyncTreenumerable<TValue>
  {
    // The storage encoding this capture holds natively -- a capture knows its shape. Null
    // only while a deferred, dimension-dispatched build has not yet decided (the layout is
    // then pinned by the first pull). Materialize's layout guarantee reuses a compliant
    // buffer and transposes a mismatched (or undecided) one.
    BufferLayout? NativeLayout { get; }
  }
}
