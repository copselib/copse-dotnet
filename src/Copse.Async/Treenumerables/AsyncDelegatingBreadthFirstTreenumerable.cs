using Copse.Core.Async;
using System;

namespace Copse.Async
{
  // Async analog of Copse.Treenumerables.DelegatingBreadthFirstTreenumerable: the single-dimension
  // sibling the narrow async operator overloads return, so a chain over an
  // IAsyncBreadthFirstTreenumerable source stays honestly breadth-first-only end to end. This is
  // the codegen source of truth for the sync twin.
  public sealed class AsyncDelegatingBreadthFirstTreenumerable<TNode> : IAsyncBreadthFirstTreenumerable<TNode>
  {
    public AsyncDelegatingBreadthFirstTreenumerable(Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _BreadthFirstTreenumeratorFactory();
  }
}
