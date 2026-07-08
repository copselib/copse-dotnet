using Copse.Core.Async;
using Copse.Linq.TreeTokenizer.BreadthFirstTree;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Copse.Linq.Async.TreeTokenizer.BreadthFirstTree
{
  // Intentionally kept though nothing consumes it yet: the symmetric pair of
  // AsyncDepthFirstTreeTokenizer (which backs ToFormattedLines / the pretty-printer). Not dead code
  // to be swept -- the asymmetry of dropping only the breadth-first half isn't worth it, and
  // this is the half a future level-order tokenizer-based serializer would want.
  public sealed class AsyncBreadthFirstTreeTokenizer<TNode> : IAsyncBreadthFirstTreeTokenizer<TNode>
  {
    public AsyncBreadthFirstTreeTokenizer(IAsyncBreadthFirstTreenumerable<TNode> treenumerable)
    {
      _Treenumerable = treenumerable;
    }

    public AsyncBreadthFirstTreeTokenizer(IAsyncEnumerable<BreadthFirstTreeToken<TNode>> enumerable)
    {
      _Enumerable = enumerable;
    }

    private readonly IAsyncBreadthFirstTreenumerable<TNode> _Treenumerable;
    private readonly IAsyncEnumerable<BreadthFirstTreeToken<TNode>> _Enumerable;

    public IAsyncEnumerator<BreadthFirstTreeToken<TNode>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
      return
        _Enumerable != null
          ? _Enumerable.GetAsyncEnumerator()
          : new AsyncBreadthFirstTreeTokenEnumerator<TNode>(_Treenumerable.GetAsyncBreadthFirstTreenumerator());
    }

    // codegen: begin sync-only
    // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    // codegen: end sync-only
  }
}
