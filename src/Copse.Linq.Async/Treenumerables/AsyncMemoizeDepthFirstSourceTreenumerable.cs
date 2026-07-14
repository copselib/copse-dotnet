using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The memo behind Memoize() for a source that only affords the DEPTH-FIRST dimension: the
  // typed upgrade op (see TRAVERSAL_DIMENSION_SPLIT.md). One preorder capture, fed by the
  // source's single dimension; depth-first replays are native playback over it, and
  // breadth-first replays ride the SAME capture cross-order (growing it as far as their
  // frontier demands) -- buying the other dimension is exactly what the memo's O(n) space
  // purchases. No completion race, no dropped buffers: the single capture is the memo.
  internal sealed class AsyncMemoizeDepthFirstSourceTreenumerable<TValue> : IAsyncLazyTreenumerableBuffer<TValue>
  {
    public AsyncMemoizeDepthFirstSourceTreenumerable(IAsyncDepthFirstTreenumerable<TValue> source)
    {
      _Buffer = new AsyncMemoizePreorderBuffer<TValue>(source.GetAsyncDepthFirstTreenumerator);
    }

    private readonly AsyncMemoizePreorderBuffer<TValue> _Buffer;

    public bool IsComplete => _Buffer.Complete;

    public int GetBufferedCount() => _Buffer.BufferedCount;

    // Both strategies drive the one capture: the capture's layout is fixed by the source's
    // dimension, and a completed capture serves both replays regardless.
    public ValueTask ConsumeAsync(TreeTraversalStrategy strategy) => _Buffer.ConsumeAsync();

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStoreDepthFirstTreenumerator<TValue, AsyncMemoizePreorderBuffer<TValue>.Handle>(
        new AsyncMemoizePreorderBuffer<TValue>.Handle(_Buffer));

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncPreorderStoreBreadthFirstTreenumerator<TValue, AsyncMemoizePreorderBuffer<TValue>.Handle>(
        new AsyncMemoizePreorderBuffer<TValue>.Handle(_Buffer));

    public ValueTask DisposeAsync() => _Buffer.DisposeAsync();
  }
}
