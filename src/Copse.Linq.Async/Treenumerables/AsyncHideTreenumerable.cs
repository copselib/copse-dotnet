using Copse.Core.Async;
using Copse.Linq.Async;

namespace Copse.Linq
{
  internal class AsyncHideTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    public AsyncHideTreenumerable(IAsyncTreenumerable<TNode> innerTreenumerable)
    {
      _InnerTreenumerable = innerTreenumerable;
    }

    private readonly IAsyncTreenumerable<TNode> _InnerTreenumerable;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => new AsyncHideTreenumerator<TNode>(_InnerTreenumerable.GetAsyncBreadthFirstTreenumerator);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncHideTreenumerator<TNode>(_InnerTreenumerable.GetAsyncDepthFirstTreenumerator);
  }
}
