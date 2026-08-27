using Copse.Core;

namespace Copse.Linq.Treenumerables
{
  // THE STRUCT-COMPOSED ARROW: the general composition law as a TYPE, and the law's one home
  // (design-docs/OPERATOR_COMPOSITION_DESIGN.md). Both legs arrive as struct type parameters,
  // so the JIT specializes and inlines the whole composed chain per visit -- user lambdas
  // remain leaf calls, but the composition PLUMBING costs nothing. That property is the
  // point: an all-delegate chain de-inlines and measurably regresses the splice. Chains nest
  // in the type: Composed<Composed<A,B>,C> -- depth is compile-time structure, not delegate
  // hops.
  //
  // The law is the algebra's one law, verbatim: the fold stops at the first
  // SkipNode-carrying result (that node left the logical tree, so the outer leg never sees
  // it and owes no value); while accepting, the value maps and strategies union.
  internal readonly struct AsyncComposedResultSelector<TSource, TMid, TResult, TInnerSelector, TOuterSelector>
    : IAsyncResultSelector<TSource, TResult>
    where TInnerSelector : struct, IAsyncResultSelector<TSource, TMid>
    where TOuterSelector : struct, IAsyncResultSelector<TMid, TResult>
  {
    public AsyncComposedResultSelector(TInnerSelector innerSelector, TOuterSelector outerSelector)
    {
      _InnerSelector = innerSelector;
      _OuterSelector = outerSelector;
    }

    private readonly TInnerSelector _InnerSelector;
    private readonly TOuterSelector _OuterSelector;

    public AsyncSelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext)
    {
      var innerResult = _InnerSelector.GetResult(nodeContext);

      if (innerResult.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        return new AsyncSelectWhereResult<TResult>(default, innerResult.Strategies);

      var outerResult = _OuterSelector.GetResult(new NodeContext<TMid>(innerResult.Node, nodeContext.Position));

      return new AsyncSelectWhereResult<TResult>(outerResult.Node, outerResult.Strategies | innerResult.Strategies);
    }
  }
}
