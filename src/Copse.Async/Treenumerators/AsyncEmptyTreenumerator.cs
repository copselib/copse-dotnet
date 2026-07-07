using Copse.Core;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Async
{
  // Async analog of Copse.Treenumerators.EmptyTreenumerator: a cursor that yields nothing. A shared
  // singleton -- it is immutable and holds no per-traversal state, so every empty tree can share it.
  internal sealed class AsyncEmptyTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    private AsyncEmptyTreenumerator()
    {
    }

    public static AsyncEmptyTreenumerator<TNode> Instance { get; } = new AsyncEmptyTreenumerator<TNode>();

    public TNode Node { get; } = default;
    public int VisitCount => default;
    public NodePosition Position => NodePosition.ForestRoot;
    public TreenumeratorMode Mode => default;

    public ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies) => new ValueTask<bool>(false);

    public ValueTask DisposeAsync() => default;
  }
}
