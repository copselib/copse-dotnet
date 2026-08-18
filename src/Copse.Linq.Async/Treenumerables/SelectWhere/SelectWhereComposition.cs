using Copse.Core;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The composition algebra, written once (design-docs/OPERATOR_COMPOSITION_DESIGN.md, "the result
  // monad"): every way two adjacent arrows compose into one, named [inner]Then[outer] in
  // execution order. The algebra is dimension-blind -- arrows never touch a treenumerator --
  // so the composite-width wrappers and both narrow-width (single-dimension) wrappers compose
  // through these same methods; the wrappers own only representation choice (which successor
  // type to build) and acquisition (which driver to hand the composed arrow).
  internal static class SelectWhereComposition
  {
    // (The composition law itself -- inner-first, SkipNode short-circuits, strategies union
    // -- lives in ComposedResultSelector, the struct-composed arrow: since the reunification
    // (2026-08-18) every general splice routes through it, Func pieces riding as
    // FuncResultSelector leaves, so the closure spellings of the general law
    // (ResultSelectorThenResultSelector, SelectThenResultSelector) went dead and were
    // deleted. This class keeps the LIGHT tier's in-tier arrows, which are closure-bound by
    // nature: only composition produces the light wrappers, and their pieces are user
    // lambdas.)

    // A projection composed onto a projection is still a projection.
    public static Func<NodeContext<TSource>, TOuterResult> SelectThenSelect<TSource, TResult, TOuterResult>(
      Func<NodeContext<TSource>, TResult> innerSelector,
      Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return nodeContext => selector(new NodeContext<TResult>(innerSelector(nodeContext), nodeContext.Position));
    }

    // A prune-after joins a projection: the predicate judges the projected value.
    public static Func<NodeContext<TSource>, SelectWhereResult<TResult>> SelectThenPruneAfter<TSource, TResult>(
      Func<NodeContext<TSource>, TResult> innerSelector,
      Func<NodeContext<TResult>, bool> predicate)
    {
      return nodeContext =>
      {
        var value = innerSelector(nodeContext);

        return new SelectWhereResult<TResult>(
          value,
          predicate(new NodeContext<TResult>(value, nodeContext.Position))
            ? NodeTraversalStrategies.SkipDescendants
            : NodeTraversalStrategies.TraverseAll);
      };
    }

    // A projection joins a prune-after: the prune predicate judges the source value (its layer
    // runs first), the selector maps it.
    public static Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> PruneAfterThenSelect<TNode, TOuterResult>(
      Func<NodeContext<TNode>, bool> predicate,
      Func<NodeContext<TNode>, TOuterResult> selector)
    {
      return nodeContext =>
      {
        var strategies = predicate(nodeContext)
          ? NodeTraversalStrategies.SkipDescendants
          : NodeTraversalStrategies.TraverseAll;

        return new SelectWhereResult<TOuterResult>(selector(nodeContext), strategies);
      };
    }

    // Prune-after over prune-after merges by predicate union -- prune when either matches.
    // Inner-first short-circuit preserves per-node lambda order; the outer predicate skips
    // nodes the inner already matched, which the purity contract permits (counts unspecified).
    public static Func<NodeContext<TNode>, bool> PruneAfterThenPruneAfter<TNode>(
      Func<NodeContext<TNode>, bool> innerPredicate,
      Func<NodeContext<TNode>, bool> outerPredicate)
    {
      return nodeContext => innerPredicate(nodeContext) || outerPredicate(nodeContext);
    }

    // A projection joins a never-rejecting chain: the value maps, the truncation strategies
    // ride (nothing in the chain can reject, so no short-circuit).
    public static Func<NodeContext<TSource>, SelectWhereResult<TOuterResult>> SelectPruneAfterThenSelect<TSource, TResult, TOuterResult>(
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> innerResultSelector,
      Func<NodeContext<TResult>, TOuterResult> selector)
    {
      return nodeContext =>
      {
        var innerResult = innerResultSelector(nodeContext);

        return new SelectWhereResult<TOuterResult>(
          selector(new NodeContext<TResult>(innerResult.Value, nodeContext.Position)),
          innerResult.Strategies);
      };
    }

    // A prune-after joins a never-rejecting chain: its predicate judges the projected value;
    // truncations union.
    public static Func<NodeContext<TSource>, SelectWhereResult<TResult>> SelectPruneAfterThenPruneAfter<TSource, TResult>(
      Func<NodeContext<TSource>, SelectWhereResult<TResult>> innerResultSelector,
      Func<NodeContext<TResult>, bool> predicate)
    {
      return nodeContext =>
      {
        var innerResult = innerResultSelector(nodeContext);

        return new SelectWhereResult<TResult>(
          innerResult.Value,
          innerResult.Strategies
            | (predicate(new NodeContext<TResult>(innerResult.Value, nodeContext.Position))
              ? NodeTraversalStrategies.SkipDescendants
              : NodeTraversalStrategies.TraverseAll));
      };
    }

    // (Cross-tier splices carry no arrow here: since the seal opened (2026-08-18) a
    // rejecting operator splices over a light wrapper through the inherited struct Compose
    // -- the chain rides as struct legs in ComposedResultSelector, the general law's one
    // home.)
  }
}
