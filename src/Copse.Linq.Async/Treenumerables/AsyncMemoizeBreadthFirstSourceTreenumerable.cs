using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // MemoizeDepthFirstSourceTreenumerable's dual: the memo for a source that only affords the
  // BREADTH-FIRST dimension. One level-order capture; breadth-first replays are native
  // playback, depth-first replays ride the same capture cross-order. Notably this is the ONLY
  // road to the depth-first dimension of a breadth-first-only source (there is no bounded
  // re-scan strategy for that direction) -- the escalation the split makes explicit.
  internal sealed class AsyncMemoizeBreadthFirstSourceTreenumerable<TValue> : IAsyncLazyTreenumerableBuffer<TValue>
  {
    public AsyncMemoizeBreadthFirstSourceTreenumerable(IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      _Buffer = new AsyncMemoizeLevelOrderBuffer<TValue>(source.GetAsyncBreadthFirstTreenumerator);
    }

    private readonly AsyncMemoizeLevelOrderBuffer<TValue> _Buffer;

    public bool IsComplete => _Buffer.Complete;

    public int GetBufferedCount(TreeTraversalStrategy strategy)
      => strategy == TreeTraversalStrategy.BreadthFirst ? _Buffer.BufferedCount : 0;

    public ValueTask ConsumeAsync(TreeTraversalStrategy strategy) => _Buffer.ConsumeAsync();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>>(
        new AsyncMemoizeLevelOrderStore<TValue>(_Buffer));

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, AsyncMemoizeLevelOrderStore<TValue>>(
        new AsyncMemoizeLevelOrderStore<TValue>(_Buffer));

    public ValueTask DisposeAsync() => _Buffer.DisposeAsync();
  }
}
