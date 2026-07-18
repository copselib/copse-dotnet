using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // SelectWhereTreenumerable's depth-first-only twin: the reified operator chain over a source
  // that affords just that dimension. Same representation, same drivers, same algebra
  // (SelectWhereComposition) -- only the successor's width differs.
  internal sealed class SelectWhereDepthFirstTreenumerable<TSource, TResult, TResultSelector> : IAsyncSelectWhereDepthFirstTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public SelectWhereDepthFirstTreenumerable(
      IAsyncDepthFirstTreenumerable<TSource> source,
      TResultSelector resultSelector,
      bool relabels)
    {
      _Source = source;
      _ResultSelector = resultSelector;
      Relabels = relabels;
    }

    private readonly IAsyncDepthFirstTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    public bool Relabels { get; }

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);

    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      return new SelectWhereDepthFirstTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          SelectWhereComposition.ResultSelectorThenResultSelector<TSource, TResult, TResultSelector, TOuterResult>(
            _ResultSelector, resultSelector)),
        Relabels | relabels);
    }
  }
}
