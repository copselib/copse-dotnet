using Copse.Async;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The memo behind Memoize(): a re-traversable, shared, lazily-growing capture of the source's
  // current shape. ONE capture: the first acquisition (or consume) pins the layout to its
  // dimension -- preorder for depth-first-first, level-order for breadth-first-first -- and
  // every replay in either dimension rides that one capture through the flat family's store
  // treenumerators (native combinations read sequentially; cross-order ones pay the accepted
  // locality tax, and over a still-growing capture an off-pin partial drain may over-pull --
  // the documented cost of the single feed; a caller who cares pins deliberately via
  // Consume/Materialize with a declared strategy). The source is enumerated AT MOST ONCE,
  // full stop: side effects upstream of the memo fire at most once per node, whichever
  // dimensions replay.
  //
  // This SUPERSEDES the dual-buffer design (one capture per dimension, the four-case serving
  // rule, completion races, ref-counted straggler replays -- see MEMOIZE_DESIGN.md's
  // superseding note, 2026-07-15): that design bought native-layout laziness in both
  // dimensions at the price of a second source enumeration, which broke at-most-once for
  // side-effecting sources and carried a page of drop/race machinery. The single capture is
  // the same model every capture operator (Invert-F's first-dimension pin, the narrow-source
  // memos) had already converged on.
  //
  // Single-threaded by contract: the buffer is append-only, but the shared feed is a live
  // treenumerator and concurrent fills would corrupt it.
  internal sealed class AsyncMemoizeTreenumerable<TValue> : IAsyncMemoizeTreenumerableBuffer<TValue>
  {
    // The pinned layout, null while fresh (nothing has pulled or consumed yet).
    public BufferLayout? NativeLayout
      => _DepthFirstCapture != null ? BufferLayout.Preorder
        : _BreadthFirstCapture != null ? BufferLayout.LevelOrder
        : (BufferLayout?)null;

    public AsyncMemoizeTreenumerable(IAsyncTreenumerable<TValue> source)
    {
      _Source = source;
    }

    private readonly IAsyncTreenumerable<TValue> _Source;

    // Exactly ONE of these two is ever created -- whichever dimension pulls (or consumes)
    // first pins the capture's layout for the memo's whole life.
    private AsyncMemoizePreorderStore<TValue> _DepthFirstCapture;
    private AsyncMemoizeLevelOrderStore<TValue> _BreadthFirstCapture;

    private bool _Disposed;

    public bool IsComplete => _DepthFirstCapture?.IsComplete == true || _BreadthFirstCapture?.IsComplete == true;

    public int GetBufferedCount()
      => _DepthFirstCapture?.BufferedCount ?? _BreadthFirstCapture?.BufferedCount ?? 0;

    public async ValueTask CompleteAsync()
    {
      // Complete the one capture; a fresh memo pins the depth-first layout (callers wanting a
      // different pin acquire a treenumerator in that dimension first -- acquisition IS the pin).
      if (_BreadthFirstCapture != null)
      {
        await _BreadthFirstCapture.CompleteAsync().ConfigureAwait(false);
        return;
      }

      await EnsureDepthFirstCapture().CompleteAsync().ConfigureAwait(false);
    }

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
    {
      if (_BreadthFirstCapture != null)
        return new AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
          new AsyncMemoizeLevelOrderStore<TValue>.Handle(_BreadthFirstCapture));

      return new AsyncPreorderStoreDepthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
        new AsyncMemoizePreorderStore<TValue>.Handle(EnsureDepthFirstCapture()));
    }

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
    {
      if (_DepthFirstCapture != null)
        return new AsyncPreorderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
          new AsyncMemoizePreorderStore<TValue>.Handle(_DepthFirstCapture));

      return new AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
        new AsyncMemoizeLevelOrderStore<TValue>.Handle(EnsureBreadthFirstCapture()));
    }

    private AsyncMemoizePreorderStore<TValue> EnsureDepthFirstCapture()
    {
      if (_DepthFirstCapture == null)
        _DepthFirstCapture = new AsyncMemoizePreorderStore<TValue>(_Source.GetAsyncDepthFirstTreenumerator);

      return _DepthFirstCapture;
    }

    // The adjacency half (the buffer re-parent): probes ride the one capture through the same
    // Handle the replays use, so a probe on a growing memo is demand (grow-precedes-read pulls
    // the feed exactly as far as the answer needs) and a probe that must pull past a retired
    // feed gets the stores' own ObjectDisposedException -- the replay rule, inherited. Probing
    // a fresh memo is consumption and pins the depth-first layout, the CompleteAsync rule.
    private IAsyncTreeTopology<TValue, int> _AdjacencyProbes;

    private IAsyncTreeTopology<TValue, int> EnsureAdjacencyProbes()
    {
      if (_AdjacencyProbes != null)
        return _AdjacencyProbes;

      if (_BreadthFirstCapture != null)
        _AdjacencyProbes = new AsyncLevelOrderAdjacencyIndex<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
          new AsyncMemoizeLevelOrderStore<TValue>.Handle(_BreadthFirstCapture));
      else
        _AdjacencyProbes = new AsyncPreorderAdjacencyIndex<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
          new AsyncMemoizePreorderStore<TValue>.Handle(EnsureDepthFirstCapture()));

      return _AdjacencyProbes;
    }

    public ValueTask<TValue> GetValueAsync(int handle) => EnsureAdjacencyProbes().GetValueAsync(handle);

    public ValueTask<ParentResult<int>> TryGetParentAsync(int handle) => EnsureAdjacencyProbes().TryGetParentAsync(handle);

    public ValueTask<ChildResult<int>> TryGetChildAtAsync(int handle, int childIndex)
      => EnsureAdjacencyProbes().TryGetChildAtAsync(handle, childIndex);

    public ValueTask<ChildResult<int>> TryGetRootAtAsync(int rootIndex) => EnsureAdjacencyProbes().TryGetRootAtAsync(rootIndex);

    // The door (walker factory design, Stage A): topology-at-birth -- the walker holds the
    // pull-through index directly; probes stay demand.
    public async ValueTask<AsyncTreeWalkerResult<TValue, int>> TryGetTreeWalkerAsync()
    {
      var topology = EnsureAdjacencyProbes();
      var rootResult = await topology.TryGetRootAtAsync(0).ConfigureAwait(false);

      return rootResult.HasChild
        ? new AsyncTreeWalkerResult<TValue, int>(new AsyncTreeWalker<TValue, int>(topology, rootResult.Child.Node))
        : default;
    }

    private AsyncMemoizeLevelOrderStore<TValue> EnsureBreadthFirstCapture()
    {
      if (_BreadthFirstCapture == null)
        _BreadthFirstCapture = new AsyncMemoizeLevelOrderStore<TValue>(_Source.GetAsyncBreadthFirstTreenumerator);

      return _BreadthFirstCapture;
    }

    // Stops all future source consumption (retires the one feed). Existing and even new replays
    // keep working over the already-captured region; any replay that needs to pull past the
    // frontier gets ObjectDisposedException (see IAsyncTreenumerableBuffer).
    public async ValueTask DisposeAsync()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      if (_DepthFirstCapture != null)
        await _DepthFirstCapture.DisposeAsync().ConfigureAwait(false);

      if (_BreadthFirstCapture != null)
        await _BreadthFirstCapture.DisposeAsync().ConfigureAwait(false);
    }
  }
}
