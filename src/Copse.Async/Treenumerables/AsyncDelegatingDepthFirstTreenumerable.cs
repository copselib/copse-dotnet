using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  // Async analog of Copse.Treenumerables.DelegatingDepthFirstTreenumerable: the single-dimension
  // sibling the narrow async operator overloads return, so a chain over an
  // IAsyncDepthFirstTreenumerable source stays honestly depth-first-only end to end. This is the
  // codegen source of truth for the sync twin.
  public sealed class AsyncDelegatingDepthFirstTreenumerable<TNode> : IAsyncDepthFirstTreenumerable<TNode>
  {
    public AsyncDelegatingDepthFirstTreenumerable(Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
    {
      _DepthFirstTreenumeratorFactory = depthFirstTreenumeratorFactory;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _DepthFirstTreenumeratorFactory;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _DepthFirstTreenumeratorFactory();
  }
}
