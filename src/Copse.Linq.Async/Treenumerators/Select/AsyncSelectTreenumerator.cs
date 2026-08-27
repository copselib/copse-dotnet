using Copse.Core;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerators
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
      Func<NodeAndPosition<TInner>, TNode> selector)
    {
      _InnerTreenumerator = innerTreenumeratorFactory();
      _Selector = selector;
    }

    private readonly IAsyncTreenumerator<TInner> _InnerTreenumerator;
    private readonly Func<NodeAndPosition<TInner>, TNode> _Selector;

    public TNode Node { get; private set; } = default;
    public int VisitCount => _InnerTreenumerator.VisitCount;
    public TreenumeratorMode Mode => _InnerTreenumerator.Mode;
    public NodePosition Position => _InnerTreenumerator.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!await _InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      var nodeAndPosition = _InnerTreenumerator.ToNodeAndPosition();

      Node = _Selector(nodeAndPosition);

      return true;
    }

    public ValueTask DisposeAsync() => _InnerTreenumerator.DisposeAsync();
  }
}
