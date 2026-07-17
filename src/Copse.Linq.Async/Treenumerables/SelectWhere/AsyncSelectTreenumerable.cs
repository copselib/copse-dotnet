using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The pure-projection wrapper. Kept distinct from SelectWhereTreenumerable deliberately: a chain of
  // nothing but Selects acquires through the light AsyncSelectTreenumerator, not the filter
  // driver -- plain operators keep their cheapest machinery; the general driver is paid only
  // when a rejecting operator joins (the representation choice IS the type split).
  internal sealed class AsyncSelectTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterTreenumerable<TResult>
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
    public bool Relabels => false;

    // The fast path: a projection composed onto a projection is still a projection, so the
    // chain keeps the light acquisition.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      var innerSelector = _Selector;

      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext => selector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position)));
    }

    // A prune-after joins: promote to the middle tier (light passthrough driver), never the
    // filter driver. The predicate judges the projected value.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      var innerSelector = _Selector;

      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source,
        nodeContext =>
        {
          var value = innerSelector(nodeContext);

          return new SelectWhereResult<TResult>(
            value,
            predicate(new NodeContext<TResult>(value, nodeContext.Position))
              ? NodeTraversalStrategies.SkipDescendants
              : NodeTraversalStrategies.TraverseAll);
        });
    }

    // The general Compose converts the representation. A projection cannot reject and carries
    // no strategies, so the selector's result stands alone -- no short-circuit, no union.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      var innerSelector = _Selector;

      return new SelectWhereTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(
          nodeContext => resultSelector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position))),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);
  }
}
