using Copse.Core;
using System;

namespace Copse.Linq.Treenumerators
{
  // Forwards a treenumerator while running an extra action when it is disposed (once).
  // ITreenumerator.Dispose is a traversal's release point, so anything acquired at
  // treenumerator creation rides this wrapper to its release -- Using's resource today,
  // Finally-style combinators tomorrow. (Cf. MemoizeTreenumerable.ReplayTreenumerator,
  // the same shape with a different payload.)
  internal sealed class DisposeActionTreenumerator<TNode> : ITreenumerator<TNode>
  {
    public DisposeActionTreenumerator(ITreenumerator<TNode> inner, Action onDispose)
    {
      _Inner = inner;
      _OnDispose = onDispose;
    }

    private readonly ITreenumerator<TNode> _Inner;
    private readonly Action _OnDispose;
    private bool _Disposed;

    public TNode Node => _Inner.Node;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategies)
      => _Inner.MoveNext(nodeTraversalStrategies);

    public void Dispose()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      try
      {
        _Inner.Dispose();
      }
      finally
      {
        _OnDispose();
      }
    }
  }
}
