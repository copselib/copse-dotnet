using Copse.Core.Async;
using System;

namespace Copse.Async.Treenumerables
{
  // Async analog of Copse.Treenumerables.DelegatingTreenumerable: a composite async tree whose two
  // dimension cursors come from injected factories. This is what AsyncTree.Defer returns -- the
  // factory indirection is where "fresh tree per acquisition" lives.
  //
  // (The narrow single-dimension siblings -- AsyncDelegatingDepthFirst/BreadthFirstTreenumerable --
  // and AsyncTree.Defer/UsingDepthFirst wait for the async flat-stream layer that motivates them: a
  // forward-only async serialized stream is the only source that affords just one async dimension.)
  public sealed class AsyncDelegatingTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    public AsyncDelegatingTreenumerable(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
      _DepthFirstTreenumeratorFactory = depthFirstTreenumeratorFactory;
    }

    private readonly Func<IAsyncTreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;
    private readonly Func<IAsyncTreenumerator<TNode>> _DepthFirstTreenumeratorFactory;

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _BreadthFirstTreenumeratorFactory();

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _DepthFirstTreenumeratorFactory();
  }
}
