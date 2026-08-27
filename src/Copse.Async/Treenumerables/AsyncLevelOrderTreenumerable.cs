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
    /// <summary>Wraps a random-access level-order store; every traversal of either dimension
    /// decodes the same store.</summary>
    public AsyncLevelOrderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncLevelOrderStoreDepthFirstTreenumerator<TNode, TStore>(_Store);

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncLevelOrderStoreBreadthFirstTreenumerator<TNode, TStore>(_Store);
  }
}
