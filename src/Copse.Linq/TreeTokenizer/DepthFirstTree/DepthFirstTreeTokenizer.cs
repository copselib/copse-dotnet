using Copse.Core;
using System.Collections;
using System.Collections.Generic;

namespace Copse.Linq.TreeTokenizer.DepthFirstTree
{
  public sealed class DepthFirstTreeTokenizer<TNode> : IDepthFirstTreeTokenizer<TNode>
  {
    public DepthFirstTreeTokenizer(IDepthFirstTreenumerable<TNode> treenumerable)
    {
      _Treenumerable = treenumerable;
    }

    public DepthFirstTreeTokenizer(IEnumerable<DepthFirstTreeToken<TNode>> enumerable)
    {
      _Enumerable = enumerable;
    }

    private readonly IDepthFirstTreenumerable<TNode> _Treenumerable;
    private readonly IEnumerable<DepthFirstTreeToken<TNode>> _Enumerable;

    public IEnumerator<DepthFirstTreeToken<TNode>> GetEnumerator()
    {
      return
        _Enumerable != null
          ? _Enumerable.GetEnumerator()
          : new DepthFirstTreeTokenEnumerator<TNode>(_Treenumerable.GetDepthFirstTreenumerator());
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}
