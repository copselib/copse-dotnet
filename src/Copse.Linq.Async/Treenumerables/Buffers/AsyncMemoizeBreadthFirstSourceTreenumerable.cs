using Copse;
using Copse.Treenumerators;
using Copse.Core;
using Copse.Linq.Stores;
using Copse.Linq.Topologies;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
{
  // MemoizeDepthFirstSourceTreenumerable's dual: the memo for a source that only affords the
  // BREADTH-FIRST dimension. One level-order capture; breadth-first replays are native
  // playback, depth-first replays ride the same capture cross-order. Notably this is the ONLY
  // road to the depth-first dimension of a breadth-first-only source (there is no bounded
  // re-scan strategy for that direction) -- the escalation the split makes explicit.
  internal sealed class AsyncMemoizeBreadthFirstSourceTreenumerable<TNode> : IAsyncMemoizeTreenumerableBuffer<TNode>
  {
    // The capture layout is fixed by the source's single dimension.
    public BufferLayout? NativeLayout => BufferLayout.LevelOrder;

    public AsyncMemoizeBreadthFirstSourceTreenumerable(IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      _Buffer = new AsyncMemoizeLevelOrderCapture<TNode>(source.GetAsyncBreadthFirstTreenumerator);
    }

    private readonly AsyncMemoizeLevelOrderCapture<TNode> _Buffer;

    public bool IsComplete => _Buffer.IsComplete;

    public int GetBufferedCount() => _Buffer.BufferedCount;

    public ValueTask CompleteAsync() => _Buffer.CompleteAsync();

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TNode, AsyncMemoizeLevelOrderCapture<TNode>.Handle>(
        new AsyncMemoizeLevelOrderCapture<TNode>.Handle(_Buffer));

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TNode, AsyncMemoizeLevelOrderCapture<TNode>.Handle>(
        new AsyncMemoizeLevelOrderCapture<TNode>.Handle(_Buffer));

    public ValueTask DisposeAsync() => _Buffer.DisposeAsync();

    // The adjacency half: probes ride the one level-order capture through the replay Handle --
    // demand on a growing feed, ObjectDisposedException past a retired one (the replay rule).
    private IAsyncTreeTopology<TNode, int> _Topology;

    private IAsyncTreeTopology<TNode, int> EnsureTopology()
      => _Topology ?? (_Topology
        = new AsyncLevelOrderAdjacencyIndex<TNode, AsyncMemoizeLevelOrderCapture<TNode>.Handle>(
          new AsyncMemoizeLevelOrderCapture<TNode>.Handle(_Buffer)));


    // The door: topology-at-birth -- the walker holds the
    // pull-through index directly; probes stay demand.
    public ValueTask<AsyncTreeWalker<TNode, int>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TNode, int>>(new AsyncTreeWalker<TNode, int>(EnsureTopology()));
  }
}
