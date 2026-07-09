using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Hide</c> and the codegen source of truth for its sync twin: forwards the inner
  /// (async) visit stream unchanged. Behind the plain <see cref="IAsyncTreenumerable{TNode}"/>
  /// contract this hides the concrete source type from callers. Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncHideTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncHideTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory)
    {
      _InnerTreenumerator = innerTreenumeratorFactory();
    }

    private readonly IAsyncTreenumerator<TNode> _InnerTreenumerator;

    public TNode Node => _InnerTreenumerator.Node;
    public int VisitCount => _InnerTreenumerator.VisitCount;
    public TreenumeratorMode Mode => _InnerTreenumerator.Mode;
    public NodePosition Position => _InnerTreenumerator.Position;

    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      return _InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies);
    }

    public ValueTask DisposeAsync() => _InnerTreenumerator.DisposeAsync();
  }
}
