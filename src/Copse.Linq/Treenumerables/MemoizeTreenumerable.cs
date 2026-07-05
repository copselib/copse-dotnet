using Copse.Core;
using Copse.Disposables;
using Copse.Linq.Treenumerators;
using Copse.Treenumerators;
using System;

namespace Copse.Linq.Treenumerables
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
  internal sealed class MemoizeTreenumerable<TValue> : ITreenumerableBuffer<TValue>
  {
    public MemoizeTreenumerable(ITreenumerable<TValue> source)
    {
      _Source = source;
    }

    private readonly ITreenumerable<TValue> _Source;

    // Each dimension buffer is paired with a RefCountDisposable whose underlying disposable
    // releases the buffer. Replays over an incomplete buffer hold a handle (GetDisposable);
    // dropping a buffer that lost the completion race is a primary Dispose -- the release
    // fires immediately if no straggler replays remain, otherwise the moment the last
    // straggler's handle is disposed.
    private MemoizeDepthFirstBuffer<TValue> _DepthFirst;
    private RefCountDisposable _DepthFirstRefCount;
    private MemoizeBreadthFirstBuffer<TValue> _BreadthFirst;
    private RefCountDisposable _BreadthFirstRefCount;

    private bool _Disposed;

    public bool IsComplete => _DepthFirst?.Complete == true || _BreadthFirst?.Complete == true;

    public int GetBufferedCount(TreeTraversalStrategy strategy)
      => strategy == TreeTraversalStrategy.DepthFirst
        ? _DepthFirst?.BufferedCount ?? 0
        : _BreadthFirst?.BufferedCount ?? 0;

    public void Consume(TreeTraversalStrategy strategy)
    {
      // The invariant outranks the argument: a retired source is never re-enumerated (a second
      // pass over an impure source could capture a DIFFERENT tree, and the memo's dimensions
      // must never disagree about the shape).
      if (IsComplete)
        return;

      if (strategy == TreeTraversalStrategy.DepthFirst)
        EnsureDepthFirstBuffer().Consume();
      else
        EnsureBreadthFirstBuffer().Consume();

      ReleaseDroppedBuffers();
    }

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
    {
      // Cases 1-2: a completed capture serves without touching the source, so no feed exists to
      // guard and the replay needs no ref-count.
      if (_DepthFirst?.Complete == true)
        return DepthFirstPlaybackOverDepthFirstBuffer(_DepthFirst);

      if (_BreadthFirst?.Complete == true)
        return DepthFirstPlaybackOverBreadthFirstBuffer(_BreadthFirst);

      // Cases 3-4: ride (creating if absent) the native buffer, extending its feed lazily.
      var buffer = EnsureDepthFirstBuffer();
      return new ReplayTreenumerator(DepthFirstPlaybackOverDepthFirstBuffer(buffer), this, _DepthFirstRefCount.GetDisposable());
    }

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
    {
      if (_BreadthFirst?.Complete == true)
        return BreadthFirstPlaybackOverBreadthFirstBuffer(_BreadthFirst);

      if (_DepthFirst?.Complete == true)
        return BreadthFirstPlaybackOverDepthFirstBuffer(_DepthFirst);

      var buffer = EnsureBreadthFirstBuffer();
      return new ReplayTreenumerator(BreadthFirstPlaybackOverBreadthFirstBuffer(buffer), this, _BreadthFirstRefCount.GetDisposable());
    }

    private MemoizeDepthFirstBuffer<TValue> EnsureDepthFirstBuffer()
    {
      if (_DepthFirst == null)
      {
        _DepthFirst = new MemoizeDepthFirstBuffer<TValue>(_Source.GetDepthFirstTreenumerator);
        _DepthFirstRefCount = new RefCountDisposable(Disposable.Create(_DepthFirst.Dispose));
      }

      return _DepthFirst;
    }

    private MemoizeBreadthFirstBuffer<TValue> EnsureBreadthFirstBuffer()
    {
      if (_BreadthFirst == null)
      {
        _BreadthFirst = new MemoizeBreadthFirstBuffer<TValue>(_Source.GetBreadthFirstTreenumerator);
        _BreadthFirstRefCount = new RefCountDisposable(Disposable.Create(_BreadthFirst.Dispose));
      }

      return _BreadthFirst;
    }

    // The four traversal-over-buffer combinations: the flat family's store treenumerators over
    // the dimension buffers (each buffer IS a store -- no engine, no child enumerators).
    // Cross-order riding is how a completed capture answers the other dimension (case 2).
    private static ITreenumerator<TValue> DepthFirstPlaybackOverDepthFirstBuffer(MemoizeDepthFirstBuffer<TValue> buffer)
      => new PreorderStoreDepthFirstTreenumerator<TValue, MemoizeDepthFirstStore<TValue>>(
        new MemoizeDepthFirstStore<TValue>(buffer));

    private static ITreenumerator<TValue> BreadthFirstPlaybackOverDepthFirstBuffer(MemoizeDepthFirstBuffer<TValue> buffer)
      => new PreorderStoreBreadthFirstTreenumerator<TValue, MemoizeDepthFirstStore<TValue>>(
        new MemoizeDepthFirstStore<TValue>(buffer));

    private static ITreenumerator<TValue> BreadthFirstPlaybackOverBreadthFirstBuffer(MemoizeBreadthFirstBuffer<TValue> buffer)
      => new LevelOrderStoreBreadthFirstTreenumerator<TValue, MemoizeBreadthFirstStore<TValue>>(
        new MemoizeBreadthFirstStore<TValue>(buffer));

    private static ITreenumerator<TValue> DepthFirstPlaybackOverBreadthFirstBuffer(MemoizeBreadthFirstBuffer<TValue> buffer)
      => new LevelOrderStoreDepthFirstTreenumerator<TValue, MemoizeBreadthFirstStore<TValue>>(
        new MemoizeBreadthFirstStore<TValue>(buffer));

    // Drop a dimension buffer that lost the completion race. Called whenever the state can
    // have changed (a dimension completing, a straggler disposing). A COMPLETE buffer is never
    // dropped: it IS the capture. Dropping is a primary Dispose on the loser's ref count: the
    // buffer is released now if no straggler replays remain, otherwise the moment the last
    // straggler's handle is disposed -- stragglers hold the buffer directly, so nulling the
    // memo's fields immediately is safe (and no new replay can route here; cases 1-2 win first).
    private void ReleaseDroppedBuffers()
    {
      if (_DepthFirst?.Complete == true
        && _BreadthFirst != null && !_BreadthFirst.Complete)
      {
        _BreadthFirstRefCount.Dispose();
        _BreadthFirst = null;
        _BreadthFirstRefCount = null;
      }

      if (_BreadthFirst?.Complete == true
        && _DepthFirst != null && !_DepthFirst.Complete)
      {
        _DepthFirstRefCount.Dispose();
        _DepthFirst = null;
        _DepthFirstRefCount = null;
      }
    }

    // Stops all future source consumption (kills both feeds). Existing and even new replays keep
    // working over the already-captured regions; any replay that needs to pull past a frontier
    // gets ObjectDisposedException (see ITreenumerableBuffer).
    public void Dispose()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      _DepthFirst?.Dispose();
      _BreadthFirst?.Dispose();
    }

    // Forwards a replay engine while making its disposal observable: releases its handle on
    // the owning dimension buffer's ref count and pokes the memo, so a dropped buffer whose
    // stragglers have all finished is released.
    private sealed class ReplayTreenumerator : ITreenumerator<TValue>
    {
      public ReplayTreenumerator(ITreenumerator<TValue> inner, MemoizeTreenumerable<TValue> owner, IDisposable bufferHandle)
      {
        _Inner = inner;
        _Owner = owner;
        _BufferHandle = bufferHandle;
      }

      private readonly ITreenumerator<TValue> _Inner;
      private readonly MemoizeTreenumerable<TValue> _Owner;
      private readonly IDisposable _BufferHandle;
      private bool _Disposed;

      public TValue Node => _Inner.Node;
      public int VisitCount => _Inner.VisitCount;
      public TreenumeratorMode Mode => _Inner.Mode;
      public NodePosition Position => _Inner.Position;

      public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies)
        => _Inner.MoveNext(nodeTraversalStrategies);

      public void Dispose()
      {
        if (_Disposed)
          return;

        _Disposed = true;

        _Inner.Dispose();
        _BufferHandle.Dispose();
        _Owner.ReleaseDroppedBuffers();
      }
    }
  }
}
