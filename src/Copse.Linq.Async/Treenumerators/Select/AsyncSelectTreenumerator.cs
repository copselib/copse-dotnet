using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Select</c> and the codegen source of truth for its sync twin: maps each node's
  /// context with the selector, forwarding the inner (async) visit stream (mode / visit count /
  /// position) unchanged. Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncSelectTreenumerator<TInner, TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncSelectTreenumerator(
      Func<IAsyncTreenumerator<TInner>> innerTreenumeratorFactory,
      Func<NodeContext<TInner>, TNode> selector)
    {
      _InnerTreenumerator = innerTreenumeratorFactory();
      _Selector = selector;
    }

    private readonly IAsyncTreenumerator<TInner> _InnerTreenumerator;
    private readonly Func<NodeContext<TInner>, TNode> _Selector;

    public TNode Node { get; private set; } = default;
    public int VisitCount => _InnerTreenumerator.VisitCount;
    public TreenumeratorMode Mode => _InnerTreenumerator.Mode;
    public NodePosition Position => _InnerTreenumerator.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!await _InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      var nodeContext = _InnerTreenumerator.ToNodeContext();

      Node = _Selector(nodeContext);

      return true;
    }

    public ValueTask DisposeAsync() => _InnerTreenumerator.DisposeAsync();
  }
}
