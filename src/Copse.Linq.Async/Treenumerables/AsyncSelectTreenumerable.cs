using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  internal sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncSelectTreenumerable<TResult>
  {
    public AsyncSelectTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TResult> selector)
    {
      _Source = source;
      _Selector = selector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, TResult> _Selector;

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);

    public IAsyncSelectTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, TOuterResult> outerSelector)
    {
      return
        new AsyncSelectTreenumerable<TSource, TOuterResult>(
          _Source,
          nodeContext => outerSelector(new NodeContext<TResult>(_Selector(nodeContext), nodeContext.Position)));
    }
  }
}
