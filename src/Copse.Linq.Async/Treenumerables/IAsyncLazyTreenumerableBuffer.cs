using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
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
    // True once the capture is complete: the whole tree is held and the source is permanently
    // retired -- no future enumeration, in either dimension, touches it again.
    bool IsComplete { get; }

    // Nodes captured so far -- there is only ever ONE capture, whose layout the first
    // acquisition (or consume) pinned. Not a progress fraction: the tree's size is unknown
    // until the capture completes.
    int GetBufferedCount();

    // Drive the capture to completion; a no-op iff IsComplete. Takes no strategy: there is
    // only ever ONE capture, and completing it is the only thing consume can mean (a fresh
    // buffer pins the depth-first layout). Pinning a specific layout is expressed the organic
    // way -- acquire a treenumerator in that dimension before consuming (acquisition is the
    // pin) -- or, for a guaranteed layout with the buffer as the deliverable,
    // Materialize(strategy).
    ValueTask ConsumeAsync();
  }
}
