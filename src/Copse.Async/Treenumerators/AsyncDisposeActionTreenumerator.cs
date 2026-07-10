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
  public sealed class AsyncDisposeActionTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncDisposeActionTreenumerator(IAsyncTreenumerator<TNode> inner, Func<ValueTask> onDispose)
    {
      _Inner = inner;
      _OnDispose = onDispose;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Func<ValueTask> _OnDispose;
    private bool _Disposed;

    public TNode Node => _Inner.Node;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
      => _Inner.MoveNextAsync(nodeTraversalStrategies);

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
