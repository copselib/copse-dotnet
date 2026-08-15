using Copse.Async;
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
  // IAsyncMemoizeTreenumerableBuffer.
  //
  // CONTRACT -- a buffer is a capture, not a computation: traversing it is effect-free and
  // idempotent. A deferred capture may run its pinned build on first use, but that build runs
  // at most once and is itself a capture (anything effectful lives upstream of it and fires at
  // most once, at capture time). The library optimizes on this -- Materialize
  // returns a compliant buffer as-is instead of re-capturing -- so an implementation whose
  // traversal has observable effects is out of contract, not merely exotic.
  //
  // WALKABLE (the buffer re-parent, design-docs/WALKABLE_CONTRACT_DESIGN.md 2026-08-12; the
  // door-only cut, design-docs/WALKER_FACTORY_DESIGN.md Stage C 2026-08-15): captures are
  // never address-poor -- a buffer is a tabulated position space, and its door binds a
  // topology whose handles are ORDINALS: the node's index in the capture's flat encoding.
  // Handle spaces are PER-CAPTURE (two captures of the same tree, or the same tree under
  // two layouts, are foreign to each other). On a still-growing capture a walker's step is
  // demand -- it forces the feed exactly as far as the answer needs; steps upward never
  // force (parents precede children in both layouts). A step that must pull past a retired
  // feed gets ObjectDisposedException, the same rule replays already live by; the buffered
  // region stays fully walkable.
  public interface IAsyncTreenumerableBuffer<TValue> : IAsyncWalkableTreenumerable<TValue, int>
  {
    // The storage encoding this capture holds natively -- a capture knows its shape. Null
    // only while a deferred, dimension-dispatched build has not yet decided (the layout is
    // then pinned by the first pull). Materialize's layout guarantee reuses a compliant
    // buffer and transposes a mismatched (or undecided) one.
    BufferLayout? NativeLayout { get; }
  }
}
