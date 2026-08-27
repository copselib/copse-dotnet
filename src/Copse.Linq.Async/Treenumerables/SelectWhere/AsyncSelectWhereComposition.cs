using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  // The LIGHT tier's composition arrows, written once (design-docs/OPERATOR_COMPOSITION_DESIGN.md,
  // "the result monad"): every way two adjacent light arrows compose into one, named
  // [inner]Then[outer] in execution order. These are closure-bound by nature -- only
  // composition produces the light wrappers, and their pieces are user lambdas.
  //
  // The GENERAL law -- inner-first, SkipNode short-circuits, strategies union -- is not here:
  // it lives in AsyncComposedResultSelector, the struct-composed arrow, which is its one home.
  // Every splice that crosses into the driver routes through that, a closure piece riding as
  // a AsyncFuncResultSelector leaf.
  //
  // The arrows are dimension-blind -- they never touch a treenumerator -- so the
  // composite-width wrappers and both narrow-width (single-dimension) wrappers compose
  // through these same methods; the wrappers own only representation choice (which successor
  // type to build) and acquisition (which driver to hand the composed arrow).
  internal static class AsyncSelectWhereComposition
  {
    // A projection composed onto a projection is still a projection.
    public static Func<NodeAndPosition<TSource>, TOuterResult> SelectThenSelect<TSource, TResult, TOuterResult>(
      Func<NodeAndPosition<TSource>, TResult> innerSelector,
      Func<NodeAndPosition<TResult>, TOuterResult> selector)
    {
      return nodeAndPosition => selector(new NodeAndPosition<TResult>(innerSelector(nodeAndPosition), nodeAndPosition.Position));
    }

    // A prune-after joins a projection: the predicate judges the projected value.
    public static Func<NodeAndPosition<TSource>, AsyncSelectWhereResult<TResult>> SelectThenPruneDescendantsWhere<TSource, TResult>(
      Func<NodeAndPosition<TSource>, TResult> innerSelector,
      Func<NodeAndPosition<TResult>, bool> predicate)
    {
      return nodeAndPosition =>
      {
        var value = innerSelector(nodeAndPosition);

        return new AsyncSelectWhereResult<TResult>(
          value,
          predicate(new NodeAndPosition<TResult>(value, nodeAndPosition.Position))
            ? NodeTraversalStrategies.PruneDescendants
            : NodeTraversalStrategies.TraverseAll);
      };
    }

    // A projection joins a prune-after: the prune predicate judges the source value (its layer
    // runs first), the selector maps it.
    public static Func<NodeAndPosition<TNode>, AsyncSelectWhereResult<TOuterResult>> PruneDescendantsWhereThenSelect<TNode, TOuterResult>(
      Func<NodeAndPosition<TNode>, bool> predicate,
      Func<NodeAndPosition<TNode>, TOuterResult> selector)
    {
      return nodeAndPosition =>
      {
        var strategies = predicate(nodeAndPosition)
          ? NodeTraversalStrategies.PruneDescendants
          : NodeTraversalStrategies.TraverseAll;

        return new AsyncSelectWhereResult<TOuterResult>(selector(nodeAndPosition), strategies);
      };
    }

    // Prune-after over prune-after merges by predicate union -- prune when either matches.
    // Inner-first short-circuit preserves per-node lambda order; the outer predicate skips
    // nodes the inner already matched, which the purity contract permits (counts unspecified).
    public static Func<NodeAndPosition<TNode>, bool> PruneDescendantsWhereThenPruneDescendantsWhere<TNode>(
      Func<NodeAndPosition<TNode>, bool> innerPredicate,
      Func<NodeAndPosition<TNode>, bool> outerPredicate)
    {
      return nodeAndPosition => innerPredicate(nodeAndPosition) || outerPredicate(nodeAndPosition);
    }

    // A projection joins a never-rejecting chain: the value maps, the truncation strategies
    // ride (nothing in the chain can reject, so no short-circuit).
    public static Func<NodeAndPosition<TSource>, AsyncSelectWhereResult<TOuterResult>> SelectPruneDescendantsWhereThenSelect<TSource, TResult, TOuterResult>(
      Func<NodeAndPosition<TSource>, AsyncSelectWhereResult<TResult>> innerResultSelector,
      Func<NodeAndPosition<TResult>, TOuterResult> selector)
    {
      return nodeAndPosition =>
      {
        var innerResult = innerResultSelector(nodeAndPosition);

        return new AsyncSelectWhereResult<TOuterResult>(
          selector(new NodeAndPosition<TResult>(innerResult.Node, nodeAndPosition.Position)),
          innerResult.Strategies);
      };
    }

    // A prune-after joins a never-rejecting chain: its predicate judges the projected value;
    // truncations union.
    public static Func<NodeAndPosition<TSource>, AsyncSelectWhereResult<TResult>> SelectPruneDescendantsWhereThenPruneDescendantsWhere<TSource, TResult>(
      Func<NodeAndPosition<TSource>, AsyncSelectWhereResult<TResult>> innerResultSelector,
      Func<NodeAndPosition<TResult>, bool> predicate)
    {
      return nodeAndPosition =>
      {
        var innerResult = innerResultSelector(nodeAndPosition);

        return new AsyncSelectWhereResult<TResult>(
          innerResult.Node,
          innerResult.Strategies
            | (predicate(new NodeAndPosition<TResult>(innerResult.Node, nodeAndPosition.Position))
              ? NodeTraversalStrategies.PruneDescendants
              : NodeTraversalStrategies.TraverseAll));
      };
    }

    // (Cross-tier splices carry no arrow here: a rejecting operator splices over a light
    // wrapper through the struct Compose door, so the chain rides as struct legs in
    // AsyncComposedResultSelector.)
  }
}
