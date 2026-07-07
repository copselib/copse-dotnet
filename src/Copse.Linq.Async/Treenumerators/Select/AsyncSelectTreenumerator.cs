using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Select</c>: maps each node's value with the selector, forwarding the inner (async)
  /// visit stream (mode / visit count / position) unchanged. Dimension-agnostic; a trivial passthrough,
  /// so it is hand-written rather than codegen'd (no cadence to keep single-sourced).
  /// </summary>
  public sealed class AsyncSelectTreenumerator<TSource, TResult> : IAsyncTreenumerator<TResult>
  {
    public AsyncSelectTreenumerator(IAsyncTreenumerator<TSource> inner, Func<TSource, TResult> selector)
    {
      _Inner = inner;
      _Selector = selector;
    }

    private readonly IAsyncTreenumerator<TSource> _Inner;
    private readonly Func<TSource, TResult> _Selector;

    public TResult Node { get; private set; } = default;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      Node = _Selector(_Inner.Node);
      return true;
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
