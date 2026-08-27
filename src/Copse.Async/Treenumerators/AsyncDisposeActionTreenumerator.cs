using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  // Forwards a treenumerator while running an extra action when it is disposed (once).
  // ITreenumerator.Dispose is a traversal's release point, so anything acquired at
  // treenumerator creation rides this wrapper to its release -- Using's resource and the
  // mirror memo's capture completion today, Finally-style combinators tomorrow.
  // (Cf. MemoizeTreenumerable.ReplayTreenumerator, the same shape with a different payload.)
  // Public for the same reason as AsyncDelegatingTreenumerable: operators outside this
  // assembly compose it.
  /// <summary>A treenumerator that forwards every member to an inner cursor and runs one extra
  /// action when disposed -- how <c>Tree.Using</c> ties a resource's release to its traversal's
  /// end.</summary>
  internal sealed class AsyncDisposeActionTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    /// <summary>Forwards every member to <paramref name="inner"/> and runs
    /// <paramref name="onDispose"/> after disposing it.</summary>
    public AsyncDisposeActionTreenumerator(IAsyncTreenumerator<TNode> inner, Func<ValueTask> onDispose)
    {
      _Inner = inner;
      _OnDispose = onDispose;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Func<ValueTask> _OnDispose;
    private bool _Disposed;

    /// <inheritdoc/>
    public TNode Node => _Inner.Node;
    /// <inheritdoc/>
    public int VisitCount => _Inner.VisitCount;
    /// <inheritdoc/>
    public TreenumeratorMode Mode => _Inner.Mode;
    /// <inheritdoc/>
    public NodePosition Position => _Inner.Position;

    /// <inheritdoc/>
    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
      => _Inner.MoveNextAsync(nodeTraversalStrategies);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      try
      {
        await _Inner.DisposeAsync().ConfigureAwait(false);
      }
      finally
      {
        await _OnDispose().ConfigureAwait(false);
      }
    }
  }
}
