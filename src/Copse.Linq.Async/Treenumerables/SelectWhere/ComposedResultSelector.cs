using Copse.Core;

namespace Copse.Linq.Async.Treenumerables
{
  // THE STRUCT-COMPOSED ARROW (the reunification gate, OPERATOR_COMPOSITION_DESIGN.md 2.9's
  // recorded exit): ResultSelectorThenResultSelector as a TYPE instead of a closure. Both
  // legs arrive as struct type parameters, so the JIT specializes and inlines the whole
  // composed chain per visit -- user lambdas remain leaf calls, but the composition PLUMBING
  // costs nothing, which is the property whose absence exiled the light tier (the
  // all-delegate FuncResultSelector chain). Chains nest in the type:
  // Composed<Composed<A,B>,C> -- depth is compile-time structure, not delegate hops.
  //
  // The law is the algebra's one law, verbatim: the fold stops at the first
  // SkipNode-carrying result (that node left the logical tree, so the outer leg never sees
  // it and owes no value); while accepting, the value maps and strategies union.
  internal readonly struct ComposedResultSelector<TSource, TMid, TResult, TInnerSelector, TOuterSelector>
    : IResultSelector<TSource, TResult>
    where TInnerSelector : struct, IResultSelector<TSource, TMid>
    where TOuterSelector : struct, IResultSelector<TMid, TResult>
  {
    public ComposedResultSelector(TInnerSelector innerSelector, TOuterSelector outerSelector)
    {
      _InnerSelector = innerSelector;
      _OuterSelector = outerSelector;
    }

    private readonly TInnerSelector _InnerSelector;
    private readonly TOuterSelector _OuterSelector;

    public SelectWhereResult<TResult> GetResult(NodeContext<TSource> nodeContext)
    {
      var innerResult = _InnerSelector.GetResult(nodeContext);

      if (innerResult.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        return new SelectWhereResult<TResult>(default, innerResult.Strategies);

      var outerResult = _OuterSelector.GetResult(new NodeContext<TMid>(innerResult.Value, nodeContext.Position));

      return new SelectWhereResult<TResult>(outerResult.Value, outerResult.Strategies | innerResult.Strategies);
    }
  }
}
