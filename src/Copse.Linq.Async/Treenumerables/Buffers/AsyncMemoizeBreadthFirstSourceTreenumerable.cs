using Copse.Async;
using Copse.Async.Treenumerators;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // MemoizeDepthFirstSourceTreenumerable's dual: the memo for a source that only affords the
  // BREADTH-FIRST dimension. One level-order capture; breadth-first replays are native
  // playback, depth-first replays ride the same capture cross-order. Notably this is the ONLY
  // road to the depth-first dimension of a breadth-first-only source (there is no bounded
  // re-scan strategy for that direction) -- the escalation the split makes explicit.
  internal sealed class AsyncMemoizeBreadthFirstSourceTreenumerable<TValue> : IAsyncMemoizeTreenumerableBuffer<TValue>
  {
    // The capture layout is fixed by the source's single dimension.
    public BufferLayout? NativeLayout => BufferLayout.LevelOrder;

    public AsyncMemoizeBreadthFirstSourceTreenumerable(IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      _Buffer = new AsyncMemoizeLevelOrderStore<TValue>(source.GetAsyncBreadthFirstTreenumerator);
    }

    private readonly AsyncMemoizeLevelOrderStore<TValue> _Buffer;

    public bool IsComplete => _Buffer.IsComplete;

    public int GetBufferedCount() => _Buffer.BufferedCount;

    public ValueTask CompleteAsync() => _Buffer.CompleteAsync();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
        new AsyncMemoizeLevelOrderStore<TValue>.Handle(_Buffer));

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
        new AsyncMemoizeLevelOrderStore<TValue>.Handle(_Buffer));

    public ValueTask DisposeAsync() => _Buffer.DisposeAsync();

    // The adjacency half: probes ride the one level-order capture through the replay Handle --
    // demand on a growing feed, ObjectDisposedException past a retired one (the replay rule).
    private IAsyncTreeTopology<TValue, int> _Topology;

    private IAsyncTreeTopology<TValue, int> EnsureTopology()
      => _Topology ?? (_Topology
        = new AsyncLevelOrderAdjacencyIndex<TValue, AsyncMemoizeLevelOrderStore<TValue>.Handle>(
          new AsyncMemoizeLevelOrderStore<TValue>.Handle(_Buffer)));

    // Probe members removed (Stage C, the cut): the contract no longer carries them.

    // The door (walker factory design, Stage A): topology-at-birth -- the walker holds the
    // pull-through index directly; probes stay demand.
    public ValueTask<AsyncTreeWalker<TValue, int>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TValue, int>>(new AsyncTreeWalker<TValue, int>(EnsureTopology()));
  }
}
