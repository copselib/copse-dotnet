using Copse.Core;
using System;

namespace Copse.Treenumerables
{
  public class DelegatingTreenumerable<TNode> : ITreenumerable<TNode>
  {
    public DelegatingTreenumerable(
      Func<ITreenumerator<TNode>> breadthFirstTreenumeratorFactory,
      Func<ITreenumerator<TNode>> depthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
      _DepthFirstTreenumeratorFactory = depthFirstTreenumeratorFactory;
    }

    private readonly Func<ITreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;
    private readonly Func<ITreenumerator<TNode>> _DepthFirstTreenumeratorFactory;

    public ITreenumerator<TNode> GetBreadthFirstTreenumerator()
      => _BreadthFirstTreenumeratorFactory();

    public ITreenumerator<TNode> GetDepthFirstTreenumerator()
      => _DepthFirstTreenumeratorFactory();
  }
}
