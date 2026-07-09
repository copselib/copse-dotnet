using Copse.Core.Async;
using Copse.Async.Treenumerators;

namespace Copse.Async.Treenumerables
{
  // Async analog of Copse.Treenumerables.EmptyTreenumerable: the empty async tree. A composite (it
  // affords both dimensions -- an empty stream is trivially both), backed by the shared empty cursor.
  internal sealed class AsyncEmptyTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    private AsyncEmptyTreenumerable()
    {
    }

    public static AsyncEmptyTreenumerable<TNode> Instance { get; } = new AsyncEmptyTreenumerable<TNode>();

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => AsyncEmptyTreenumerator<TNode>.Instance;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => AsyncEmptyTreenumerator<TNode>.Instance;
  }
}
