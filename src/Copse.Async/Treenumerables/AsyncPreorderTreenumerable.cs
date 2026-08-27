using Copse.Async.Stores;
using Copse.Async.Treenumerators;
using Copse.Core.Async;

namespace Copse.Async.Treenumerables
{
  // Codegen source of truth for the sync twin (Copse.Treenumerables.PreorderTreenumerable).
  /// <summary>
  /// An async tree stored in flat preorder form. Any <see cref="IAsyncPreorderStore{TNode}"/>
  /// -- a completed capture, or one still growing from an async feed -- becomes a full
  /// <see cref="IAsyncTreenumerable{TNode}"/>: depth-first traversal is native playback,
  /// breadth-first rides the same store cross-order.
  /// </summary>
  public sealed class AsyncPreorderTreenumerable<TNode, TStore> : IAsyncTreenumerable<TNode>
    where TStore : IAsyncPreorderStore<TNode>
  {
    /// <summary>Wraps a random-access preorder store; every traversal of either dimension
    /// decodes the same store.</summary>
    public AsyncPreorderTreenumerable(TStore store)
    {
      _Store = store;
    }

    private readonly TStore _Store;

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncPreorderStoreDepthFirstTreenumerator<TNode, TStore>(_Store);

    /// <inheritdoc/>
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncPreorderStoreBreadthFirstTreenumerator<TNode, TStore>(_Store);
  }
}
