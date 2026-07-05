using Copse.Core;
using System.Collections;
using System.Collections.Generic;

namespace Copse.Linq.TreeTokenizer.BreadthFirstTree
{
  public sealed class BreadthFirstTreeTokenizer<TNode> : IBreadthFirstTreeTokenizer<TNode>
  {
    public BreadthFirstTreeTokenizer(IBreadthFirstTreenumerable<TNode> treenumerable)
    {
      _Treenumerable = treenumerable;
    }

    public BreadthFirstTreeTokenizer(IEnumerable<BreadthFirstTreeToken<TNode>> enumerable)
    {
      _Enumerable = enumerable;
    }

    private readonly IBreadthFirstTreenumerable<TNode> _Treenumerable;
    private readonly IEnumerable<BreadthFirstTreeToken<TNode>> _Enumerable;

    public IEnumerator<BreadthFirstTreeToken<TNode>> GetEnumerator()
    {
      return
        _Enumerable != null
          ? _Enumerable.GetEnumerator()
          : new BreadthFirstTreeTokenEnumerator<TNode>(_Treenumerable.GetBreadthFirstTreenumerator());
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}
