using Copse.Core;
using Copse.Treenumerables;
using System;

namespace Copse.Linq
{
  public static class TreenumerableFactory
  {
    public static ITreenumerable<TNode> Create<TNode>(
      Func<ITreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<ITreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new DelegatingTreenumerable<TNode>(
        breadthFirstTreenumeratorFactory,
        depthFirstTreenumeratorFactory);

    // The single-dimension factories behind the narrow operator overloads (see
    // TRAVERSAL_DIMENSION_SPLIT.md): a chain over a narrow source stays narrow.
    public static IDepthFirstTreenumerable<TNode> CreateDepthFirst<TNode>(
      Func<ITreenumerator<TNode>> depthFirstTreenumeratorFactory)
      => new DelegatingDepthFirstTreenumerable<TNode>(depthFirstTreenumeratorFactory);

    public static IBreadthFirstTreenumerable<TNode> CreateBreadthFirst<TNode>(
      Func<ITreenumerator<TNode>> breadthFirstTreenumeratorFactory)
      => new DelegatingBreadthFirstTreenumerable<TNode>(breadthFirstTreenumeratorFactory);
  }
}
