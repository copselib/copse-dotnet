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

    public IAsyncTreenumerable<TOuterResult> FuseStage<TOuterResult>(
      Func<NodeContext<TResult>, FusionVerdict<TOuterResult>> stage,
      bool stageRelabels)
    {
      var innerSelector = _Selector;

      return FusedTreenumerable.Create<TSource, TOuterResult, FuncVerdictSelector<TSource, TOuterResult>>(
        _Source,
        new FuncVerdictSelector<TSource, TOuterResult>(nodeContext =>
          stage(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position))),
        stageRelabels);
    }

    public IAsyncTreenumerable<TOuterResult> FuseProjection<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      var innerSelector = _Selector;

      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext => selector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position)));
    }
  }
}
