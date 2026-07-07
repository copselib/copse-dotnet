using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>Hide</c>: forwards the inner (async) visit stream unchanged. Behind the plain
  /// <see cref="IAsyncTreenumerable{TNode}"/> contract this hides the concrete source type from
  /// callers. Dimension-agnostic; a pure passthrough, hand-written (no cadence).
  /// </summary>
  public sealed class AsyncHideTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncHideTreenumerator(IAsyncTreenumerator<TNode> inner)
    {
      _Inner = inner;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;

    public TNode Node => _Inner.Node;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
      => _Inner.MoveNextAsync(nodeTraversalStrategies);

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
