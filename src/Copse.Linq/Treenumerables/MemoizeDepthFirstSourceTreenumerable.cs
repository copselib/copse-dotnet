using Copse.Core;
using Copse.Linq.Treenumerators;
using Copse.Treenumerators;

namespace Copse.Linq.Treenumerables
{
  // The memo behind Memoize() for a source that only affords the DEPTH-FIRST dimension: the
  // typed upgrade op (see TRAVERSAL_DIMENSION_SPLIT.md). One preorder capture, fed by the
  // source's single dimension; depth-first replays are native playback over it, and
  // breadth-first replays ride the SAME capture cross-order (growing it as far as their
  // frontier demands) -- buying the other dimension is exactly what the memo's O(n) space
  // purchases. No completion race, no dropped buffers: the single capture is the memo.
  internal sealed class MemoizeDepthFirstSourceTreenumerable<TValue> : ITreenumerableBuffer<TValue>
  {
    public MemoizeDepthFirstSourceTreenumerable(IDepthFirstTreenumerable<TValue> source)
    {
      _Buffer = new MemoizeDepthFirstBuffer<TValue>(source.GetDepthFirstTreenumerator);
    }

    private readonly MemoizeDepthFirstBuffer<TValue> _Buffer;

    public bool IsComplete => _Buffer.Complete;

    public int GetBufferedCount(TreeTraversalStrategy strategy)
      => strategy == TreeTraversalStrategy.DepthFirst ? _Buffer.BufferedCount : 0;

    // Both strategies drive the one capture: the capture's layout is fixed by the source's
    // dimension, and a completed capture serves both replays regardless.
    public void Consume(TreeTraversalStrategy strategy) => _Buffer.Consume();

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new PreorderStoreDepthFirstTreenumerator<TValue, MemoizeDepthFirstStore<TValue>>(
        new MemoizeDepthFirstStore<TValue>(_Buffer));

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new PreorderStoreBreadthFirstTreenumerator<TValue, MemoizeDepthFirstStore<TValue>>(
        new MemoizeDepthFirstStore<TValue>(_Buffer));

    public void Dispose() => _Buffer.Dispose();
  }
}
