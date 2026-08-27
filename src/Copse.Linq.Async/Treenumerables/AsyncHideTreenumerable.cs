using Copse.Linq.Treenumerators;
using Copse.Core;
using Copse.Linq;

namespace Copse.Linq
{
  // The barrier itself: a plain-contract wrapper, so the result claims no composition door and
  // nothing downstream can compose into or reroute on the source. That property belongs to THIS
  // type, not to the treenumerator -- which is why HideScope.Treenumerable can forward
  // acquisition untouched and still be a complete barrier.
  internal class AsyncHideTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    public AsyncHideTreenumerable(IAsyncTreenumerable<TNode> innerTreenumerable, HideScope scope)
    {
      _InnerTreenumerable = innerTreenumerable;
      _Scope = scope;
    }

    private readonly IAsyncTreenumerable<TNode> _InnerTreenumerable;
    private readonly HideScope _Scope;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
      => _Scope == HideScope.Treenumerator
        ? new AsyncHideTreenumerator<TNode>(_InnerTreenumerable.GetAsyncBreadthFirstTreenumerator)
        : _InnerTreenumerable.GetAsyncBreadthFirstTreenumerator();

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => _Scope == HideScope.Treenumerator
        ? new AsyncHideTreenumerator<TNode>(_InnerTreenumerable.GetAsyncDepthFirstTreenumerator)
        : _InnerTreenumerable.GetAsyncDepthFirstTreenumerator();
  }
}
