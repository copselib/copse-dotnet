using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
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

    // Select's emission boundary is the identity on positions, so every appended operator may
    // cross it: both Where flavors fuse into the projection-carrying Where drivers (the fused
    // SelectWhere is an INSTANTIATION of the plain Where machinery, not a new driver), and an
    // appended Select composes selectors.
    public IAsyncTreenumerable<TResult> FuseWhere(Func<TResult, bool> predicate) =>
      FuseContextWhere(nodeContext => predicate(nodeContext.Node));

    public IAsyncTreenumerable<TResult> FusePositionalWhere(Func<TResult, NodePosition, bool> predicate) =>
      FuseContextWhere(nodeContext => predicate(nodeContext.Node, nodeContext.Position));

    public IAsyncTreenumerable<TOuterResult> FuseSelect<TOuterResult>(Func<NodeContext<TResult>, TOuterResult> outerSelector)
    {
      var innerSelector = _Selector;

      return
        new AsyncSelectTreenumerable<TSource, TOuterResult>(
          _Source,
          nodeContext => outerSelector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position)));
    }

    private IAsyncTreenumerable<TResult> FuseContextWhere(Func<NodeContext<TResult>, bool> contextPredicate) =>
      AsyncTreenumerableFactory.Create(
        () => new AsyncWhereBreadthFirstTreenumerator<TSource, TResult>(
          _Source.GetAsyncBreadthFirstTreenumerator, _Selector, contextPredicate, NodeTraversalStrategies.SkipNode),
        () => new AsyncWhereDepthFirstTreenumerator<TSource, TResult>(
          _Source.GetAsyncDepthFirstTreenumerator, _Selector, contextPredicate, NodeTraversalStrategies.SkipNode));
  }
}
