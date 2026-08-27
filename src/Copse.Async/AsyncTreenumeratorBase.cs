using Copse.Core;
using System.Threading.Tasks;

namespace Copse
{
  // Hand-written twin of Copse.TreenumeratorBase (see its comment for why the base is a maintained
  // pair rather than codegen'd). Async disposal, no finalizer -- structurally parallel to the sync
  // base so the codegen'd operators that derive from either land identically after the await-strip.
  /// <summary>
  /// The base class for implementing an async treenumerator: derive, implement
  /// <see cref="OnMoveNextAsync"/> to advance and set the four state properties, and the base
  /// handles exhaustion (<see cref="EnumerationFinished"/>), disposal idempotence, and the
  /// synchronous fast path (when a pull completes inline, no async state machine runs).
  /// Override <see cref="OnDisposingAsync"/> for teardown.
  /// </summary>
  public abstract class AsyncTreenumeratorBase<TNode> : IAsyncTreenumerator<TNode>
  {
    /// <inheritdoc/>
    public TNode Node { get; protected set; } = default;

    /// <inheritdoc/>
    public int VisitCount { get; protected set; } = 0;

    /// <inheritdoc/>
    public NodePosition Position { get; protected set; } = NodePosition.ForestRoot;

    /// <inheritdoc/>
    public TreenumeratorMode Mode { get; protected set; } = default;

    /// <summary>True once a pull has answered false; every later pull answers false without
    /// calling <see cref="OnMoveNextAsync"/> again.</summary>
    protected bool EnumerationFinished { get; private set; }


    // NOT async: when the derived pull completes synchronously (a buffered store, an in-memory
    // tree -- the overwhelmingly common case) the whole move is ordinary method calls, no state
    // machine; the continuation below is entered only when the pull genuinely suspends. This one
    // fast path removes a per-pull state machine from EVERY derived treenumerator at once.
    /// <inheritdoc/>
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

    /// <summary>Advances the traversal one visit and sets <see cref="Node"/>,
    /// <see cref="VisitCount"/>, <see cref="Position"/>, and <see cref="Mode"/>; answers
    /// false when the traversal is exhausted. The base never calls this after exhaustion
    /// or disposal.</summary>
    protected abstract ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategy);

    #region IAsyncDisposable

    /// <summary>True once <see cref="DisposeAsync"/> has run; pulls answer false from then on.</summary>
    protected bool Disposed { get; private set; } = false;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
      if (Disposed)
        return;

      await OnDisposingAsync().ConfigureAwait(false);
      Disposed = true;
    }

    /// <summary>Teardown hook, run exactly once, before <see cref="Disposed"/> is set.</summary>
    protected virtual ValueTask OnDisposingAsync() => default;

    #endregion IAsyncDisposable
  }
}
