using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Do</c> and the codegen source of truth for its sync twin: runs a side effect on
  /// every emitted visit, forwarding the inner (async) visit stream unchanged. Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncDoTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncDoTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Action<NodeVisit<TNode>> onNext)
    {
      _InnerTreenumerator = innerTreenumeratorFactory();
      _OnNext = onNext;
    }

    private readonly IAsyncTreenumerator<TNode> _InnerTreenumerator;
    private readonly Action<NodeVisit<TNode>> _OnNext;

    public TNode Node => _InnerTreenumerator.Node;
    public int VisitCount => _InnerTreenumerator.VisitCount;
    public TreenumeratorMode Mode => _InnerTreenumerator.Mode;
    public NodePosition Position => _InnerTreenumerator.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!await _InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      _OnNext?.Invoke(_InnerTreenumerator.ToNodeVisit());

      return true;
    }

    public ValueTask DisposeAsync() => _InnerTreenumerator.DisposeAsync();
  }
}
