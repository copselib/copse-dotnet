using Copse.Async.Stores;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync twin; AsyncPreorderTreenumerable's structural dual.
  /// <summary>
  /// An async tree stored in flat level-order form. Any
  /// <see cref="IAsyncLevelOrderStore{TValue}"/> becomes a full
  /// <see cref="IAsyncTreenumerable{TValue}"/>: breadth-first traversal is native playback,
  /// depth-first rides the same store cross-order.
  /// </summary>
  public sealed class AsyncLevelOrderTreenumerable<TValue, TStore> : IAsyncTreenumerable<TValue>
    where TStore : IAsyncLevelOrderStore<TValue>
  {
    public AsyncLevelOrderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);
  }
}
