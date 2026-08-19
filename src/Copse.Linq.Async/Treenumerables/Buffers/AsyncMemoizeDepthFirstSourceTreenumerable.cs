using Copse.Async;
using Copse.Async.Treenumerators;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The memo behind Memoize() for a source that only affords the DEPTH-FIRST dimension: the
  // typed upgrade op (see TRAVERSAL_DIMENSION_SPLIT.md). One preorder capture, fed by the
  // source's single dimension; depth-first replays are native playback over it, and
  // breadth-first replays ride the SAME capture cross-order (growing it as far as their
  // frontier demands) -- buying the other dimension is exactly what the memo's O(n) space
  // purchases. No completion race, no dropped buffers: the single capture is the memo.
  internal sealed class AsyncMemoizeDepthFirstSourceTreenumerable<TValue> : IAsyncMemoizeTreenumerableBuffer<TValue>
  {
    // The capture layout is fixed by the source's single dimension.
    public BufferLayout? NativeLayout => BufferLayout.Preorder;

    public AsyncMemoizeDepthFirstSourceTreenumerable(IAsyncDepthFirstTreenumerable<TValue> source)
    {
      _Buffer = new AsyncMemoizePreorderStore<TValue>(source.GetAsyncDepthFirstTreenumerator);
    }

    private readonly AsyncMemoizePreorderStore<TValue> _Buffer;

    public bool IsComplete => _Buffer.IsComplete;

    public int GetBufferedCount() => _Buffer.BufferedCount;

    // Both strategies drive the one capture: the capture's layout is fixed by the source's
    // dimension, and a completed capture serves both replays regardless.
    public ValueTask CompleteAsync() => _Buffer.CompleteAsync();

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStoreDepthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
        new AsyncMemoizePreorderStore<TValue>.Handle(_Buffer));

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncPreorderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
        new AsyncMemoizePreorderStore<TValue>.Handle(_Buffer));

    public ValueTask DisposeAsync() => _Buffer.DisposeAsync();

    // The adjacency half: probes ride the one preorder capture through the replay Handle --
    // demand on a growing feed, ObjectDisposedException past a retired one (the replay rule).
    private IAsyncTreeTopology<TValue, int> _Topology;

    private IAsyncTreeTopology<TValue, int> EnsureTopology()
      => _Topology ?? (_Topology
        = new AsyncPreorderAdjacencyIndex<TValue, AsyncMemoizePreorderStore<TValue>.Handle>(
          new AsyncMemoizePreorderStore<TValue>.Handle(_Buffer)));

    // Probe members removed (Stage C, the cut): the contract no longer carries them.

    // The door (walker factory design, Stage A): topology-at-birth -- the walker holds the
    // pull-through index directly; probes stay demand.
    public async ValueTask<Option<AsyncTreeWalker<TValue, int>>> TryGetTreeWalkerAsync()
    {
      var topology = EnsureTopology();
      var rootResult = await topology.TryGetRootAtAsync(0).ConfigureAwait(false);

      return rootResult.HasValue
        ? new Option<AsyncTreeWalker<TValue, int>>(new AsyncTreeWalker<TValue, int>(topology, rootResult.Value.Node))
        : default;
    }
  }
}
