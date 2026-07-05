using Copse.Core;
using System;

namespace Copse.Treenumerables
{
  // DelegatingTreenumerable's single-dimension sibling: the wrapper the narrow operator
  // overloads return, so a chain over an IBreadthFirstTreenumerable source stays honestly
  // breadth-first-only end to end (see TRAVERSAL_DIMENSION_SPLIT.md).
  public class DelegatingBreadthFirstTreenumerable<TNode> : IBreadthFirstTreenumerable<TNode>
  {
    public DelegatingBreadthFirstTreenumerable(Func<ITreenumerator<TNode>> breadthFirstTreenumeratorFactory)
    {
      _BreadthFirstTreenumeratorFactory = breadthFirstTreenumeratorFactory;
    }

    private readonly Func<ITreenumerator<TNode>> _BreadthFirstTreenumeratorFactory;

    public ITreenumerator<TNode> GetBreadthFirstTreenumerator()
      => _BreadthFirstTreenumeratorFactory();
  }
}
