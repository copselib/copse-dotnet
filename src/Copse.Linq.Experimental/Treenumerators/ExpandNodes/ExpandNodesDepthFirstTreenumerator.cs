using Copse.Core;
using System;

namespace Copse.Linq.Experimental.Treenumerators.ExpandNodes
{
  internal class ExpandNodesDepthFirstTreenumerator<TSource, TExpandedNode, TResult>
    : TreenumeratorBase<TResult>
  {
    public ExpandNodesDepthFirstTreenumerator(
      Func<ITreenumerator<TSource>> innerTreenumeratorFactory,
      Func<NodeAndPosition<TSource>, bool> predicate,
      Func<NodeAndPosition<TSource>, ITreenumerable<TExpandedNode>> nodeExpander,
      Func<NodeAndPosition<TSource>, NodeAndPosition<TExpandedNode>, TResult> selector)
    {
      _InnerTreenumerator = innerTreenumeratorFactory();
      _Predicate = predicate;
      _NodeExpander = nodeExpander;
      _Selector = selector;
    }

    private readonly ITreenumerator<TSource> _InnerTreenumerator;
    private readonly Func<NodeAndPosition<TSource>, bool> _Predicate;
    private readonly Func<NodeAndPosition<TSource>, ITreenumerable<TExpandedNode>> _NodeExpander;
    private readonly Func<NodeAndPosition<TSource>, NodeAndPosition<TExpandedNode>, TResult> _Selector;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!_InnerTreenumerator.MoveNext(nodeTraversalStrategies))
        return false;

      throw new NotImplementedException();
    }

    protected override void OnDisposing()
    {
      _InnerTreenumerator?.Dispose();
    }
  }
}
