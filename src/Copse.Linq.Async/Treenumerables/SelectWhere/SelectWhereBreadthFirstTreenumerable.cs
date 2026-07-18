using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // SelectWhereTreenumerable's breadth-first-only twin: the reified operator chain over a source
  // that affords just that dimension. Same representation, same drivers, same algebra
  // (SelectWhereComposition) -- only the successor's width differs.
  internal sealed class SelectWhereBreadthFirstTreenumerable<TSource, TResult, TResultSelector> : IAsyncSelectWhereBreadthFirstTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public SelectWhereBreadthFirstTreenumerable(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      TResultSelector resultSelector,
      bool relabels)
    {
      _Source = source;
      _ResultSelector = resultSelector;
      Relabels = relabels;
    }

    private readonly IAsyncBreadthFirstTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    public bool Relabels { get; }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncBreadthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      return new SelectWhereBreadthFirstTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          SelectWhereComposition.ResultSelectorThenResultSelector<TSource, TResult, TResultSelector, TOuterResult>(
            _ResultSelector, resultSelector)),
        Relabels | relabels);
    }
  }
}
