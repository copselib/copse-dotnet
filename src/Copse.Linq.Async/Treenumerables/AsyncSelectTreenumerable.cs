using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The pure-projection wrapper. Kept distinct from ComposableTreenumerable deliberately: a chain of
  // nothing but Selects acquires through the light AsyncSelectTreenumerator, not the filter
  // driver -- plain operators keep their cheapest machinery; the general driver is paid only
  // when a filter joins (the representation choice IS this type split). The projection fast
  // path (ComposeProjection) is this type's capability, declared here alone.
  internal sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncComposableProjection<TResult>
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

    // The fast path: a projection composed onto a projection is still a projection, so the
    // chain keeps the light acquisition.
    public IAsyncTreenumerable<TOuterResult> ComposeProjection<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      var innerSelector = _Selector;

      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext => selector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position)));
    }

    // The general stage converts the representation. A projection cannot reject and carries
    // no strategies, so the stage's result stands alone -- no short-circuit, no union.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, CompositionResult<TOuterResult>> stage,
      bool relabels)
    {
      var innerSelector = _Selector;

      return new ComposableTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          nodeContext => stage(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position))),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);
  }
}
