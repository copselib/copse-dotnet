using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The pure-projection wrapper. Kept distinct from FusedTreenumerable deliberately: a chain of
  // nothing but Selects acquires through the light AsyncSelectTreenumerator, not the filter
  // driver -- plain operators keep their cheapest machinery; the general driver is paid only
  // when a filter joins (FuseStage converts, FuseProjection preserves the representation).
  internal sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncFusableTreenumerable<TResult>
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

    // Projections never relabel.
    public bool ContainsRelabelingStage => false;

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);

    // The bind, with the representation choice the stage's purity makes possible: a projection
    // composes selectors and stays on the light Select treenumerator; a filter converts the
    // chain to the verdict driver.
    public IAsyncTreenumerable<TOuterResult> Fuse<TOuterResult>(FusionStage<TResult, TOuterResult> stage)
    {
      var innerSelector = _Selector;

      if (stage.IsProjection)
      {
        var projection = stage.Projection;

        return new AsyncSelectTreenumerable<TSource, TOuterResult>(
          _Source,
          nodeContext => projection(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position)));
      }

      var verdict = stage.Verdict;

      return FusedTreenumerable.Create<TSource, TOuterResult, FuncVerdictSelector<TSource, TOuterResult>>(
        _Source,
        new FuncVerdictSelector<TSource, TOuterResult>(nodeContext =>
          verdict(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position))),
        stage.Relabels);
    }
  }
}
