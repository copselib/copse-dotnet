using Copse.Async.Stores;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync twin; AsyncPreorderTreenumerable's structural dual.
  /// <summary>
  /// An async tree stored in flat level-order form. Any
  /// <see cref="IAsyncLevelOrderStore{TNode}"/> becomes a full
  /// <see cref="IAsyncTreenumerable{TNode}"/>: breadth-first traversal is native playback,
  /// depth-first rides the same store cross-order.
  /// </summary>
  public sealed class AsyncLevelOrderTreenumerable<TNode, TStore> : IAsyncTreenumerable<TNode>
    where TStore : IAsyncLevelOrderStore<TNode>
  {
    public AsyncLevelOrderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TNode, TStore>(_Store);

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TNode, TStore>(_Store);
  }
}
