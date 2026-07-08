using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.DepthFirstTree;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Copse.Linq.Async.TreeTokenizer.DepthFirstTree
{
  public sealed class AsyncDepthFirstTreeTokenizer<TNode> : IAsyncDepthFirstTreeTokenizer<TNode>
  {
    public AsyncDepthFirstTreeTokenizer(IAsyncDepthFirstTreenumerable<TNode> treenumerable)
    {
      _Treenumerable = treenumerable;
    }

    public AsyncDepthFirstTreeTokenizer(IAsyncEnumerable<DepthFirstTreeToken<TNode>> enumerable)
    {
      _Enumerable = enumerable;
    }

    private readonly IAsyncDepthFirstTreenumerable<TNode> _Treenumerable;
    private readonly IAsyncEnumerable<DepthFirstTreeToken<TNode>> _Enumerable;

    public IAsyncEnumerator<DepthFirstTreeToken<TNode>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
      return
        _Enumerable != null
          ? _Enumerable.GetAsyncEnumerator()
          : new AsyncDepthFirstTreeTokenEnumerator<TNode>(_Treenumerable.GetAsyncDepthFirstTreenumerator());
    }

    // codegen: begin sync-only
    // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    // codegen: end sync-only
  }
}
