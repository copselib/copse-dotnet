using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The middle representation tier: a composed chain of projections and prune-afters. Every
  // result preserves labels and never carries SkipNode, so the chain runs on the light
  // passthrough driver -- no promotion machinery, no path state, one driver class for both
  // dimensions. Only composition produces this wrapper (plain Select and plain PruneAfter
  // keep their own cheapest machinery), so the arrow is delegate-bound by nature and needs
  // no struct seam.
  internal sealed class AsyncSelectPruneAfterTreenumerable<TSource, TResult> : IAsyncSelectPruneAfterTreenumerable<TResult>
  {
    public AsyncSelectPruneAfterTreenumerable(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> resultSelector)
    {
      _Source = source;
      _ResultSelector = resultSelector;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly Func<NodeContext<TSource>, SelectWhereResult<TResult>> _ResultSelector;

    // The tier invariant: nothing in the chain moves a label.
    public bool Relabels => false;

    // A projection composes in-tier: the value maps, the truncation strategies ride (nothing
    // in the chain can reject, so no short-circuit).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> selector)
    {
      var innerResultSelector = _ResultSelector;

      return new AsyncSelectPruneAfterTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext =>
        {
          var innerResult = innerResultSelector(nodeContext);

          return new SelectWhereResult<TOuterResult>(
            selector(new NodeContext<TResult>(innerResult.Value, nodeContext.Position)),
            innerResult.Strategies);
        });
    }

    // A prune-after composes in-tier: its predicate judges the projected value; truncations
    // union.
    public IAsyncTreenumerable<TResult> ComposePruneAfter(Func<NodeContext<TResult>, bool> predicate)
    {
      var innerResultSelector = _ResultSelector;

      return new AsyncSelectPruneAfterTreenumerable<TSource, TResult>(
        _Source,
        nodeContext =>
        {
          var innerResult = innerResultSelector(nodeContext);

          return new SelectWhereResult<TResult>(
            innerResult.Value,
            innerResult.Strategies
              | (predicate(new NodeContext<TResult>(innerResult.Value, nodeContext.Position))
                ? NodeTraversalStrategies.SkipDescendants
                : NodeTraversalStrategies.TraverseAll));
        });
    }

    // A rejecting operator arrived: convert to the general representation. The inner arrow
    // never rejects, so no short-circuit -- values map, strategies union.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
    {
      var innerResultSelector = _ResultSelector;

      return new SelectWhereTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(nodeContext =>
        {
          var innerResult = innerResultSelector(nodeContext);
          var outerResult = resultSelector(new NodeContext<TResult>(innerResult.Value, nodeContext.Position));

          return new SelectWhereResult<TOuterResult>(outerResult.Value, outerResult.Strategies | innerResult.Strategies);
        }),
        relabels);
    }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectPruneAfterTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);
  }
}
