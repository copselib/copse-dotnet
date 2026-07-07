using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Do</c>: runs a side effect on every emitted visit, forwarding the inner (async)
  /// visit stream unchanged. Dimension-agnostic; a trivial passthrough, hand-written (no cadence).
  /// </summary>
  public sealed class AsyncDoTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncDoTreenumerator(IAsyncTreenumerator<TNode> inner, Action<NodeVisit<TNode>> onNext)
    {
      _Inner = inner;
      _OnNext = onNext;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Action<NodeVisit<TNode>> _OnNext;

    public TNode Node => _Inner.Node;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      _OnNext?.Invoke(new NodeVisit<TNode>(_Inner.Mode, _Inner.Node, _Inner.VisitCount, _Inner.Position));
      return true;
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
