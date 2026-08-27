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
  /// <summary>
  /// A buffer still backed by a live source feed: the lazily-growing capture <c>Memoize</c>
  /// returns. Because it IS a treenumerable buffer it composes anywhere a capture is
  /// expected, but the fluent surface sees only the non-disposable base -- keep this
  /// reference to dispose it. Disposing retires the feed: enumerators already replaying keep
  /// working over the captured region, and one that needs data beyond it throws
  /// <see cref="ObjectDisposedException"/>.
  /// </summary>
  public interface IAsyncMemoizeTreenumerableBuffer<TNode> : IAsyncTreenumerableBuffer<TNode>, IAsyncDisposable
  {
    /// <summary>True once the capture is complete: the whole tree is held and the source is
    /// permanently retired -- no future enumeration, in either dimension, touches it
    /// again.</summary>
    bool IsComplete { get; }

    /// <summary>The number of nodes captured so far. Not a progress fraction: the tree's
    /// size is unknown until the capture completes.</summary>
    int GetBufferedCount();

    /// <summary>Drives the capture to completion -- the transition <see cref="IsComplete"/>
    /// reports; a no-op if already complete. A fresh buffer pins the depth-first layout; to
    /// pin a specific layout, acquire a treenumerator in that dimension first (acquisition
    /// is the pin).</summary>
    ValueTask CompleteAsync();
  }
}
