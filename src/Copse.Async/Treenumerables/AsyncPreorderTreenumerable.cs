using Copse.Async.Stores;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync twin (Copse.Treenumerables.PreorderTreenumerable).
  /// <summary>
  /// An async tree stored in flat preorder form. Any <see cref="IAsyncPreorderStore{TValue}"/>
  /// -- a completed capture, or one still growing from an async feed -- becomes a full
  /// <see cref="IAsyncTreenumerable{TValue}"/>: depth-first traversal is native playback,
  /// breadth-first rides the same store cross-order.
  /// </summary>
  public sealed class AsyncPreorderTreenumerable<TValue, TStore> : IAsyncTreenumerable<TValue>
    where TStore : IAsyncPreorderStore<TValue>
  {
    public AsyncPreorderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStoreDepthFirstTreenumerator<TValue, TStore>(_Store);

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator()
      => new AsyncPreorderStoreBreadthFirstTreenumerator<TValue, TStore>(_Store);
  }
}
