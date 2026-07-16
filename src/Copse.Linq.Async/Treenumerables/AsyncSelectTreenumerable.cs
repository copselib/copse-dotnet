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
  // when a filter joins. Because this wrapper contains only projections, every hook fuses:
  // its emission boundary is the identity on positions.
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

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncBreadthFirstTreenumerator, _Selector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncSelectTreenumerator<TSource, TResult>(_Source.GetAsyncDepthFirstTreenumerator, _Selector);

    public IAsyncTreenumerable<TResult> FuseWhere(Func<TResult, bool> predicate)
    {
      var innerSelector = _Selector;

      return new FusedTreenumerable<TSource, TResult>(
        _Source,
        nodeContext =>
        {
          var projectedNode = innerSelector(nodeContext);

          return predicate(projectedNode)
            ? FusionVerdict<TResult>.Accept(projectedNode)
            : FusionVerdict<TResult>.Reject(NodeTraversalStrategies.SkipNode);
        },
        containsRelabelingStage: true);
    }

    public IAsyncTreenumerable<TResult> FusePositionalWhere(Func<TResult, NodePosition, bool> predicate)
    {
      var innerSelector = _Selector;

      return new FusedTreenumerable<TSource, TResult>(
        _Source,
        nodeContext =>
        {
          var projectedNode = innerSelector(nodeContext);

          return predicate(projectedNode, nodeContext.Position)
            ? FusionVerdict<TResult>.Accept(projectedNode)
            : FusionVerdict<TResult>.Reject(NodeTraversalStrategies.SkipNode);
        },
        containsRelabelingStage: true);
    }

    public IAsyncTreenumerable<TOuterResult> FuseSelect<TOuterResult>(Func<TResult, TOuterResult> selector)
    {
      var innerSelector = _Selector;

      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext => selector(innerSelector(nodeContext)));
    }

    public IAsyncTreenumerable<TOuterResult> FusePositionalSelect<TOuterResult>(Func<TResult, NodePosition, TOuterResult> selector)
    {
      var innerSelector = _Selector;

      return new AsyncSelectTreenumerable<TSource, TOuterResult>(
        _Source,
        nodeContext => selector(innerSelector(nodeContext), nodeContext.Position));
    }
  }
}
