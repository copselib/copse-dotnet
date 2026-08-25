using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The buffer Materialize returns over a LIVE MEMO source (2026-08-10, the lazy-Materialize
  // reshape): the memo's one capture COMPLETED IN BULK at the first pull, presented as the
  // non-disposable capture marker. Nothing is enumerated before that pull -- an unconsumed
  // instance holds exactly what the unconsumed memo already held -- and the feed retires inside
  // the settle, which is what distinguishes this from the memo itself (Memoize grows
  // incrementally and needs disposing; Materialize delivers the whole capture at one deferred
  // moment and nothing to dispose after it).
  //
  // The layout guarantee rides the same settle. With a requested layout the pin lands AT
  // CONSTRUCTION -- acquiring a memo treenumerator in the requested dimension creates the
  // capture and pulls no nodes -- so an intervening consumer of the shared memo cannot pin it
  // the other way; the held pin treenumerator is retired inside the settle. If the shared
  // memo's history already pinned the other layout, the settle TRANSPOSES from the completed
  // capture (source untouched, a new instance), and replays serve from the transposed store.
  //
  // Single-threaded by contract, like the memo it wraps.
  internal sealed class AsyncMaterializeTreenumerable<TNode> : IAsyncTreenumerableBuffer<TNode>
  {
    public AsyncMaterializeTreenumerable(IAsyncMemoizeTreenumerableBuffer<TNode> memo, BufferLayout? requestedLayout)
    {
      _Memo = memo;
      _RequestedLayout = requestedLayout;

      if (requestedLayout == BufferLayout.Preorder)
        _LayoutPinHold = memo.GetAsyncDepthFirstTreenumerator();
      else if (requestedLayout == BufferLayout.LevelOrder)
        _LayoutPinHold = memo.GetAsyncBreadthFirstTreenumerator();
    }

    private readonly IAsyncMemoizeTreenumerableBuffer<TNode> _Memo;
    private readonly BufferLayout? _RequestedLayout;
    private IAsyncTreenumerator<TNode> _LayoutPinHold;
    private IAsyncTreenumerableBuffer<TNode> _Settled;

    internal IAsyncMemoizeTreenumerableBuffer<TNode> Memo => _Memo;

    // With a requested layout this is the guarantee, reported from the call onward; otherwise a
    // live view of the memo's pin (null while nothing has pulled or consumed yet).
    public BufferLayout? NativeLayout
    {
      get
      {
        if (_RequestedLayout != null)
          return _RequestedLayout;

        return _Settled != null ? _Settled.NativeLayout : _Memo.NativeLayout;
      }
    }

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => _Settled != null
        ? _Settled.GetAsyncDepthFirstTreenumerator()
        : new AsyncMaterializeTreenumerator<TNode>(
          this,
          TreeTraversalStrategy.DepthFirst,
          _Memo.GetAsyncDepthFirstTreenumerator());

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => _Settled != null
        ? _Settled.GetAsyncBreadthFirstTreenumerator()
        : new AsyncMaterializeTreenumerator<TNode>(
          this,
          TreeTraversalStrategy.BreadthFirst,
          _Memo.GetAsyncBreadthFirstTreenumerator());

    // The one deferred moment: retire the pin hold, complete the memo's capture in bulk, and --
    // only when a requested layout lost the pin race to the shared memo's own history --
    // transpose from the completed capture. Idempotent; every replay pulls through it.
    internal async ValueTask<IAsyncTreenumerableBuffer<TNode>> SettleAsync()
    {
      if (_Settled != null)
        return _Settled;

      if (_LayoutPinHold != null)
      {
        await _LayoutPinHold.DisposeAsync().ConfigureAwait(false);
        _LayoutPinHold = null;
      }

      await _Memo.CompleteAsync().ConfigureAwait(false);

      if (_RequestedLayout == null || _Memo.NativeLayout == _RequestedLayout)
      {
        _Settled = _Memo;
        return _Settled;
      }

      if (_RequestedLayout == BufferLayout.Preorder)
      {
        // The memo is complete (the CompleteAsync above), so its count presizes the transpose
        // capture (the counted fast path -- final arrays exact, no chunked build buffer).
        var preorderStore = await AsyncPreorderCapture.CaptureFromAsync(_Memo, _Memo.GetBufferedCount()).ConfigureAwait(false);

        _Settled = new AsyncTreenumerableBuffer<TNode>(
          new AsyncPreorderTreenumerable<TNode, AsyncPreorderArrayStore<TNode>>(preorderStore),
          BufferLayout.Preorder,
          new AsyncPreorderArrayTopology<TNode>(preorderStore));

        return _Settled;
      }

      var levelOrderStore = await AsyncLevelOrderCapture.CaptureFromAsync(_Memo, _Memo.GetBufferedCount()).ConfigureAwait(false);

      _Settled = new AsyncTreenumerableBuffer<TNode>(
        new AsyncLevelOrderTreenumerable<TNode, AsyncLevelOrderArrayStore<TNode>>(levelOrderStore),
        BufferLayout.LevelOrder,
        new AsyncLevelOrderArrayTopology<TNode>(levelOrderStore));

      return _Settled;
    }

    // The adjacency half rides the settle: probing IS consumption, so a probe on an unsettled
    // instance completes the memo's capture exactly as the first stream pull would, then
    // delegates to the settled buffer's own probes (the memo's, or the transposed capture's).
    // The door (walker factory design, Stage A): the settled capture manufactures the walker,
    // so the stance rides the settled topology directly.
    public async ValueTask<AsyncTreeWalker<TNode, int>> GetTreeWalkerAsync()
      => await (await SettleAsync().ConfigureAwait(false)).GetTreeWalkerAsync().ConfigureAwait(false);

    // Probe members removed (Stage C, the cut): the contract no longer carries them.
  }
}
