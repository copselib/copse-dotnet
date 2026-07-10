using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Async
{
  // Hand-written twin of Copse.TreenumeratorBase (see its comment for why the base is a maintained
  // pair rather than codegen'd). Async disposal, no finalizer -- structurally parallel to the sync
  // base so the codegen'd operators that derive from either land identically after the await-strip.
  public abstract class AsyncTreenumeratorBase<TNode> : IAsyncTreenumerator<TNode>
  {
    public TNode Node { get; protected set; } = default;

    public int VisitCount { get; protected set; } = 0;

    public NodePosition Position { get; protected set; } = NodePosition.ForestRoot;

    public TreenumeratorMode Mode { get; protected set; } = default;

    protected bool EnumerationFinished { get; private set; }


    // NOT async: when the derived pull completes synchronously (a buffered store, an in-memory
    // tree -- the overwhelmingly common case) the whole move is ordinary method calls, no state
    // machine; the continuation below is entered only when the pull genuinely suspends. This one
    // fast path removes a per-pull state machine from EVERY derived treenumerator at once.
    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategy)
    {
      if (Disposed || EnumerationFinished)
        return new ValueTask<bool>(false);

      var moved = OnMoveNextAsync(nodeTraversalStrategy);

      if (!moved.IsCompletedSuccessfully)
        return AwaitThenFinishMoveNextAsync(moved);

      if (moved.Result)
        return new ValueTask<bool>(true);

      EnumerationFinished = true;

      return new ValueTask<bool>(false);
    }

    private async ValueTask<bool> AwaitThenFinishMoveNextAsync(ValueTask<bool> pendingMove)
    {
      if (await pendingMove.ConfigureAwait(false))
        return true;

      EnumerationFinished = true;

      return false;
    }

    protected abstract ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategy);

    #region IAsyncDisposable

    protected bool Disposed { get; private set; } = false;

    public async ValueTask DisposeAsync()
    {
      if (Disposed)
        return;

      await OnDisposingAsync().ConfigureAwait(false);
      Disposed = true;
    }

    protected virtual ValueTask OnDisposingAsync() => default;

    #endregion IAsyncDisposable
  }
}
