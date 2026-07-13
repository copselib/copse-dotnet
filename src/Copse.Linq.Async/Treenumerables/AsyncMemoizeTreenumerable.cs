using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Disposables;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The memo behind Memoize(): a re-traversable, shared, lazily-growing capture of the source's
  // current shape. Each traversal dimension gets its own capture -- a dimension buffer holding
  // that dimension's feed and native-layout arrays, created on the first replay request in that
  // dimension -- because a linear buffer is only as lazy as the order of the stream that fills it
  // (see MEMOIZE_DESIGN.md, "the governing principle").
  //
  // A replay is one of the flat family's four store treenumerators (DFT/BFT x
  // preorder/level-order store) decoding a dimension buffer directly -- native combinations read
  // sequentially, cross-order ones pay the accepted locality tax -- so every
  // NodeTraversalStrategies flag and all position bookkeeping come from machinery shared with
  // every other flat-stored tree; the only memoize-specific moving part is which buffer serves
  // the request:
  //
  //   1. the native dimension's buffer is complete  -> ride it natively;
  //   2. the OTHER dimension's buffer is complete   -> ride it cross-order (correct, O(N); the
  //      locality tax is accepted) -- the source is never touched once either capture completes;
  //   3. the native buffer exists but is partial    -> ride it, extending its feed lazily;
  //   4. no native buffer, other absent or partial  -> create the native buffer (the only path
  //      that opens a second source enumeration, and it requires partial work in BOTH dimensions).
  //
  // Once either dimension completes, the other (incomplete) buffer is DROPPED, not completed: it
  // takes no new replays (cases 1-2 win first) and is released once its ref-count of outstanding
  // replay enumerators drains to zero -- in-flight stragglers keep their own buffer and feed, so
  // no mid-enumeration cut-over is ever attempted. Replay Dispose is therefore semantically
  // load-bearing (it is what releases a dropped buffer), consistent with child-enumerator
  // Dispose signaling skips to the engine.
  //
  // Single-threaded by contract: the buffers are append-only, but the shared feeds are live
  // treenumerators and concurrent fills would corrupt them.
  internal sealed class AsyncMemoizeTreenumerable<TValue> : IAsyncLazyTreenumerableBuffer<TValue>
  {
    public AsyncMemoizeTreenumerable(IAsyncTreenumerable<TValue> source)
    {
      _Source = source;
    }

    private readonly IAsyncTreenumerable<TValue> _Source;

    // Each dimension buffer is paired with a ref-count disposable whose underlying disposable
    // releases the buffer. Replays over an incomplete buffer hold a handle (GetDisposable);
    // dropping a buffer that lost the completion race is a primary Dispose -- the release
    // fires immediately if no straggler replays remain, otherwise the moment the last
    // straggler's handle is disposed.
    private AsyncMemoizePreorderBuffer<TValue> _DepthFirst;
    private AsyncRefCountDisposable _DepthFirstRefCount;
    private AsyncMemoizeLevelOrderBuffer<TValue> _BreadthFirst;
    private AsyncRefCountDisposable _BreadthFirstRefCount;

    private bool _Disposed;

    public bool IsComplete => _DepthFirst?.Complete == true || _BreadthFirst?.Complete == true;

    public int GetBufferedCount(TreeTraversalStrategy strategy)
      => strategy == TreeTraversalStrategy.DepthFirst
        ? _DepthFirst?.BufferedCount ?? 0
        : _BreadthFirst?.BufferedCount ?? 0;

    public async ValueTask ConsumeAsync(TreeTraversalStrategy strategy)
    {
      // The invariant outranks the argument: a retired source is never re-enumerated (a second
      // pass over an impure source could capture a DIFFERENT tree, and the memo's dimensions
      // must never disagree about the shape).
      if (IsComplete)
        return;

      if (strategy == TreeTraversalStrategy.DepthFirst)
        await EnsureDepthFirstBuffer().ConsumeAsync().ConfigureAwait(false);
      else
        await EnsureBreadthFirstBuffer().ConsumeAsync().ConfigureAwait(false);

      await ReleaseDroppedBuffersAsync().ConfigureAwait(false);
    }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
    {
      // Cases 1-2: a completed capture serves without touching the source, so no feed exists to
      // guard and the replay needs no ref-count.
      if (_DepthFirst?.Complete == true)
        return DepthFirstPlaybackOverDepthFirstBuffer(_DepthFirst);

      if (_BreadthFirst?.Complete == true)
        return DepthFirstPlaybackOverBreadthFirstBuffer(_BreadthFirst);

      // Cases 3-4: ride (creating if absent) the native buffer, extending its feed lazily.
      var buffer = EnsureDepthFirstBuffer();
      return new AsyncReplayTreenumerator(DepthFirstPlaybackOverDepthFirstBuffer(buffer), this, _DepthFirstRefCount.GetDisposable());
    }

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
    {
      if (_BreadthFirst?.Complete == true)
        return BreadthFirstPlaybackOverBreadthFirstBuffer(_BreadthFirst);

      if (_DepthFirst?.Complete == true)
        return BreadthFirstPlaybackOverDepthFirstBuffer(_DepthFirst);

      var buffer = EnsureBreadthFirstBuffer();
      return new AsyncReplayTreenumerator(BreadthFirstPlaybackOverBreadthFirstBuffer(buffer), this, _BreadthFirstRefCount.GetDisposable());
    }

    private AsyncMemoizePreorderBuffer<TValue> EnsureDepthFirstBuffer()
    {
      if (_DepthFirst == null)
      {
        _DepthFirst = new AsyncMemoizePreorderBuffer<TValue>(_Source.GetAsyncDepthFirstTreenumerator);
        _DepthFirstRefCount = new AsyncRefCountDisposable(AsyncDisposable.Create(_DepthFirst.DisposeAsync));
      }

      return _DepthFirst;
    }

    private AsyncMemoizeLevelOrderBuffer<TValue> EnsureBreadthFirstBuffer()
    {
      if (_BreadthFirst == null)
      {
        _BreadthFirst = new AsyncMemoizeLevelOrderBuffer<TValue>(_Source.GetAsyncBreadthFirstTreenumerator);
        _BreadthFirstRefCount = new AsyncRefCountDisposable(AsyncDisposable.Create(_BreadthFirst.DisposeAsync));
      }

      return _BreadthFirst;
    }

    // The four traversal-over-buffer combinations: the flat family's store treenumerators over
    // the dimension buffers (each buffer IS a store -- no engine, no child enumerators).
    // Cross-order riding is how a completed capture answers the other dimension (case 2).
    private static IAsyncTreenumerator<TValue> DepthFirstPlaybackOverDepthFirstBuffer(AsyncMemoizePreorderBuffer<TValue> buffer)
      => new AsyncPreorderStoreDepthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>>(
        new AsyncMemoizePreorderStore<TValue>(buffer));

    private static IAsyncTreenumerator<TValue> BreadthFirstPlaybackOverDepthFirstBuffer(AsyncMemoizePreorderBuffer<TValue> buffer)
      => new AsyncPreorderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>>(
        new AsyncMemoizePreorderStore<TValue>(buffer));

    private static IAsyncTreenumerator<TValue> BreadthFirstPlaybackOverBreadthFirstBuffer(AsyncMemoizeLevelOrderBuffer<TValue> buffer)
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>>(
        new AsyncMemoizeLevelOrderStore<TValue>(buffer));

    private static IAsyncTreenumerator<TValue> DepthFirstPlaybackOverBreadthFirstBuffer(AsyncMemoizeLevelOrderBuffer<TValue> buffer)
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>>(
        new AsyncMemoizeLevelOrderStore<TValue>(buffer));

    // Drop a dimension buffer that lost the completion race. Called whenever the state can
    // have changed (a dimension completing, a straggler disposing). A COMPLETE buffer is never
    // dropped: it IS the capture. Dropping is a primary Dispose on the loser's ref count: the
    // buffer is released now if no straggler replays remain, otherwise the moment the last
    // straggler's handle is disposed -- stragglers hold the buffer directly, so nulling the
    // memo's fields immediately is safe (and no new replay can route here; cases 1-2 win first).
    private async ValueTask ReleaseDroppedBuffersAsync()
    {
      if (_DepthFirst?.Complete == true
        && _BreadthFirst != null && !_BreadthFirst.Complete)
      {
        var refCount = _BreadthFirstRefCount;
        _BreadthFirst = null;
        _BreadthFirstRefCount = null;
        await refCount.DisposeAsync().ConfigureAwait(false);
      }

      if (_BreadthFirst?.Complete == true
        && _DepthFirst != null && !_DepthFirst.Complete)
      {
        var refCount = _DepthFirstRefCount;
        _DepthFirst = null;
        _DepthFirstRefCount = null;
        await refCount.DisposeAsync().ConfigureAwait(false);
      }
    }

    // Stops all future source consumption (kills both feeds). Existing and even new replays keep
    // working over the already-captured regions; any replay that needs to pull past a frontier
    // gets ObjectDisposedException (see IAsyncTreenumerableBuffer).
    public async ValueTask DisposeAsync()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      if (_DepthFirst != null)
        await _DepthFirst.DisposeAsync().ConfigureAwait(false);

      if (_BreadthFirst != null)
        await _BreadthFirst.DisposeAsync().ConfigureAwait(false);
    }

    // Forwards a replay engine while making its disposal observable: releases its handle on
    // the owning dimension buffer's ref count and pokes the memo, so a dropped buffer whose
    // stragglers have all finished is released.
    private sealed class AsyncReplayTreenumerator : IAsyncTreenumerator<TValue>
    {
      public AsyncReplayTreenumerator(IAsyncTreenumerator<TValue> inner, AsyncMemoizeTreenumerable<TValue> owner, IAsyncDisposable bufferHandle)
      {
        _Inner = inner;
        _Owner = owner;
        _BufferHandle = bufferHandle;
      }

      private readonly IAsyncTreenumerator<TValue> _Inner;
      private readonly AsyncMemoizeTreenumerable<TValue> _Owner;
      private readonly IAsyncDisposable _BufferHandle;
      private bool _Disposed;

      public TValue Node => _Inner.Node;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;

      public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
        => _Inner.MoveNextAsync(nodeTraversalStrategies);

      public async ValueTask DisposeAsync()
      {
        if (_Disposed)
          return;

        _Disposed = true;

        await _Inner.DisposeAsync().ConfigureAwait(false);
        await _BufferHandle.DisposeAsync().ConfigureAwait(false);
        await _Owner.ReleaseDroppedBuffersAsync().ConfigureAwait(false);
      }
    }
  }
}
