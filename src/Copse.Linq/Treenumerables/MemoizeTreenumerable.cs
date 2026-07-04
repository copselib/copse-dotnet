using Copse.Core;
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
  // A replay is the STANDARD engine (DepthFirstTreenumerator / BreadthFirstTreenumerator) riding
  // a lazy child enumerator over a dimension buffer, so every NodeTraversalStrategies flag and
  // all position bookkeeping come from machinery that already exists; the only memoize-specific
  // moving part is which buffer serves the request:
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

    private MemoizeDepthFirstBuffer<TValue> _DepthFirst;
    private MemoizeBreadthFirstBuffer<TValue> _BreadthFirst;

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
        return DepthFirstEngineOverDepthFirstBuffer(_DepthFirst);

      if (_BreadthFirst?.Complete == true)
        return DepthFirstEngineOverBreadthFirstBuffer(_BreadthFirst);

      // Cases 3-4: ride (creating if absent) the native buffer, extending its feed lazily.
      var buffer = EnsureDepthFirstBuffer();
      buffer.RegisterReplay();
      return new ReplayTreenumerator(DepthFirstEngineOverDepthFirstBuffer(buffer), this, buffer.UnregisterReplay);
    }

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
    {
      if (_BreadthFirst?.Complete == true)
        return BreadthFirstEngineOverBreadthFirstBuffer(_BreadthFirst);

      if (_DepthFirst?.Complete == true)
        return BreadthFirstEngineOverDepthFirstBuffer(_DepthFirst);

      var buffer = EnsureBreadthFirstBuffer();
      buffer.RegisterReplay();
      return new ReplayTreenumerator(BreadthFirstEngineOverBreadthFirstBuffer(buffer), this, buffer.UnregisterReplay);
    }

    private MemoizeDepthFirstBuffer<TValue> EnsureDepthFirstBuffer()
      => _DepthFirst ?? (_DepthFirst = new MemoizeDepthFirstBuffer<TValue>(_Source.GetDepthFirstTreenumerator));

    private MemoizeBreadthFirstBuffer<TValue> EnsureBreadthFirstBuffer()
      => _BreadthFirst ?? (_BreadthFirst = new MemoizeBreadthFirstBuffer<TValue>(_Source.GetBreadthFirstTreenumerator));

    // The four engine-over-buffer combinations. Each dimension buffer serves EITHER engine
    // through the same child enumerator -- cross-order riding is how a completed capture answers
    // the other dimension (case 2).
    private static ITreenumerator<TValue> DepthFirstEngineOverDepthFirstBuffer(MemoizeDepthFirstBuffer<TValue> buffer)
      => new DepthFirstTreenumerator<TValue, int, MemoizeDepthFirstChildEnumerator<TValue>>(
        buffer.EnumerateRootIndices(),
        nodeContext => new MemoizeDepthFirstChildEnumerator<TValue>(buffer, nodeContext.Node),
        buffer.GetValue);

    private static ITreenumerator<TValue> BreadthFirstEngineOverDepthFirstBuffer(MemoizeDepthFirstBuffer<TValue> buffer)
      => new BreadthFirstTreenumerator<TValue, int, MemoizeDepthFirstChildEnumerator<TValue>>(
        buffer.EnumerateRootIndices(),
        nodeContext => new MemoizeDepthFirstChildEnumerator<TValue>(buffer, nodeContext.Node),
        buffer.GetValue);

    private static ITreenumerator<TValue> BreadthFirstEngineOverBreadthFirstBuffer(MemoizeBreadthFirstBuffer<TValue> buffer)
      => new BreadthFirstTreenumerator<TValue, int, MemoizeBreadthFirstChildEnumerator<TValue>>(
        buffer.EnumerateRootIndices(),
        nodeContext => new MemoizeBreadthFirstChildEnumerator<TValue>(buffer, nodeContext.Node),
        buffer.GetValue);

    private static ITreenumerator<TValue> DepthFirstEngineOverBreadthFirstBuffer(MemoizeBreadthFirstBuffer<TValue> buffer)
      => new DepthFirstTreenumerator<TValue, int, MemoizeBreadthFirstChildEnumerator<TValue>>(
        buffer.EnumerateRootIndices(),
        nodeContext => new MemoizeBreadthFirstChildEnumerator<TValue>(buffer, nodeContext.Node),
        buffer.GetValue);

    // Release a dropped dimension buffer -- one that lost the completion race -- once no replay
    // still needs it. Called whenever the state can have changed (a dimension completing, a
    // straggler disposing). A COMPLETE buffer is never released: it IS the capture.
    private void ReleaseDroppedBuffers()
    {
      if (_DepthFirst?.Complete == true
        && _BreadthFirst != null && !_BreadthFirst.Complete && _BreadthFirst.OutstandingReplays == 0)
      {
        _BreadthFirst.Dispose();
        _BreadthFirst = null;
      }

      if (_BreadthFirst?.Complete == true
        && _DepthFirst != null && !_DepthFirst.Complete && _DepthFirst.OutstandingReplays == 0)
      {
        _DepthFirst.Dispose();
        _DepthFirst = null;
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

    // Forwards a replay engine while making its disposal observable: unregisters from the
    // owning dimension buffer and lets the memo release a dropped buffer whose stragglers have
    // all finished.
    private sealed class ReplayTreenumerator : ITreenumerator<TValue>
    {
      public ReplayTreenumerator(ITreenumerator<TValue> inner, MemoizeTreenumerable<TValue> owner, Action unregister)
      {
        _Inner = inner;
        _Owner = owner;
        _Unregister = unregister;
      }

      private readonly ITreenumerator<TValue> _Inner;
      private readonly MemoizeTreenumerable<TValue> _Owner;
      private readonly Action _Unregister;
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
        _Unregister();
        _Owner.ReleaseDroppedBuffers();
      }
    }
  }
}
