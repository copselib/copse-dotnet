using Copse.Core;
using Copse.Linq.Treenumerators;
using Copse.Treenumerators;

namespace Copse.Linq.Treenumerables
{
  // MemoizeDepthFirstSourceTreenumerable's dual: the memo for a source that only affords the
  // BREADTH-FIRST dimension. One level-order capture; breadth-first replays are native
  // playback, depth-first replays ride the same capture cross-order. Notably this is the ONLY
  // road to the depth-first dimension of a breadth-first-only source (there is no bounded
  // re-scan strategy for that direction) -- the escalation the split makes explicit.
  internal sealed class MemoizeBreadthFirstSourceTreenumerable<TValue> : ITreenumerableBuffer<TValue>
  {
    public MemoizeBreadthFirstSourceTreenumerable(IBreadthFirstTreenumerable<TValue> source)
    {
      _Buffer = new MemoizeBreadthFirstBuffer<TValue>(source.GetBreadthFirstTreenumerator);
    }

    private readonly MemoizeBreadthFirstBuffer<TValue> _Buffer;

    public bool IsComplete => _Buffer.Complete;

    public int GetBufferedCount(TreeTraversalStrategy strategy)
      => strategy == TreeTraversalStrategy.BreadthFirst ? _Buffer.BufferedCount : 0;

    public void Consume(TreeTraversalStrategy strategy) => _Buffer.Consume();

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
      => new LevelOrderStoreBreadthFirstTreenumerator<TValue, MemoizeBreadthFirstStore<TValue>>(
        new MemoizeBreadthFirstStore<TValue>(_Buffer));

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
      => new LevelOrderStoreDepthFirstTreenumerator<TValue, MemoizeBreadthFirstStore<TValue>>(
        new MemoizeBreadthFirstStore<TValue>(_Buffer));

    public void Dispose() => _Buffer.Dispose();
  }
}
