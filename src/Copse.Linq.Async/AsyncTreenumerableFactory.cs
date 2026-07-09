using Copse.Async.Treenumerables;
using Copse.Core.Async;
using System;

namespace Copse.Linq
{
  public static class AsyncTreenumerableFactory
  {
    public static IAsyncTreenumerable<TNode> Create<TNode>(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new AsyncDelegatingTreenumerable<TNode>(
        breadthFirstTreenumeratorFactory,
        depthFirstTreenumeratorFactory);

    // The single-dimension factories behind the narrow operator overloads (see
    // TRAVERSAL_DIMENSION_SPLIT.md): a chain over a narrow source stays narrow.
    public static IAsyncDepthFirstTreenumerable<TNode> CreateDepthFirst<TNode>(
      Func<IAsyncTreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new AsyncDelegatingDepthFirstTreenumerable<TNode>(depthFirstTreenumeratorFactory);

    public static IAsyncBreadthFirstTreenumerable<TNode> CreateBreadthFirst<TNode>(
      Func<IAsyncTreenumerator<TNode>> breadthFirstTreenumeratorFactory)
      => new AsyncDelegatingBreadthFirstTreenumerable<TNode>(breadthFirstTreenumeratorFactory);
  }
}
