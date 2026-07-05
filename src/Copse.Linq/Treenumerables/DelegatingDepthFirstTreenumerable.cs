using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // DelegatingTreenumerable's single-dimension sibling: the wrapper the narrow operator
  // overloads return, so a chain over an IDepthFirstTreenumerable source stays honestly
  // depth-first-only end to end (see TRAVERSAL_DIMENSION_SPLIT.md).
  public class DelegatingDepthFirstTreenumerable<TNode> : IDepthFirstTreenumerable<TNode>
  {
    public DelegatingDepthFirstTreenumerable(Func<ITreenumerator<TNode>> depthFirstTreenumeratorFactory)
    {
      _DepthFirstTreenumeratorFactory = depthFirstTreenumeratorFactory;
    }

    private readonly Func<ITreenumerator<TNode>> _DepthFirstTreenumeratorFactory;

    public ITreenumerator<TNode> GetDepthFirstTreenumerator()
      => _DepthFirstTreenumeratorFactory();
  }
}
