using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The fold tier of the leaffix pair: map-then-combine up the tree. Every node's
    /// accumulation starts at <paramref name="nodeSelector"/> (the map -- each node's own
    /// contribution, and the fold's starting value; a leaf is the projection unchanged), and
    /// each child's completed accumulation is combined in by <paramref name="accumulator"/> --
    /// one child at a time, in sibling order, so non-commutative folds like concatenation are
    /// well-defined. Decomposable aggregations (sum, count, max, concat) are one lambda each;
    /// a combine that must SEE the nodes involved takes the context overload, and anything
    /// needing all children at once (median, top-k) or boundary-only contributions (leaf
    /// count) belongs to LeaffixDispatch, the sibling-complete tier this operator is sugar
    /// over. The delegation's measured premium (dashboard, 2026-08-02): ~8ns per internal node
    /// -- ~8% on a degenerate chain, ~1-2% on branching shapes -- at identical allocation;
    /// accepted deliberately (well under the Invert specialization bar), the bespoke build
    /// stays retired, and the paired benchmark rows guard the gap.
    ///
    /// <para>Callbacks run during the deferred build, once per node (the selector) and once
    /// per child edge (the accumulator); only the sibling fold order is specified --
    /// invocation timing relative to the source walk is not, so callbacks should be pure.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because a leaffix scan
    /// MANUFACTURES owned O(n) storage: a root's value IS its whole subtree's aggregate, so
    /// the source is fully consumed before the first result visit can be published. Deferred:
    /// construction is pinned to the first treenumerator acquisition. The source is consumed
    /// depth-first only, so a streamed narrow source can leaffix.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, nodeSelector, FoldSurvey(nodeSelector, accumulator));

    /// <summary>
    /// The context overload: the combine also sees the FOLDING node (the parent absorbing the
    /// child), for rules like weighting each child's rollup by the folding node's own factor.
    /// If the rule needs more than that -- other children, child identity -- it is a survey:
    /// use LeaffixDispatch.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, nodeSelector, FoldSurvey(nodeSelector, accumulator));

    /// <summary>The breadth-first-only source overload; the disclosure-rule escalation is LeaffixDispatch's.</summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, nodeSelector, FoldSurvey(nodeSelector, accumulator));

    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, nodeSelector, FoldSurvey(nodeSelector, accumulator));

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator);

    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator);

    // The fold expressed as a survey: start at the node's own projection, combine each child's
    // completed accumulation in sibling order (the view enumerates children left-to-right).
    // This is the whole delegation -- the scan owns no build; LeaffixDispatch's is the one
    // buffer-producing leaffix build.
    private static Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> FoldSurvey<TSource, TAccumulate>(
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (nodeContext, children) =>
      {
        var accumulate = nodeSelector(nodeContext);
        foreach (var childAccumulate in children)
          accumulate = accumulator(accumulate, childAccumulate);
        return accumulate;
      };

    private static Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> FoldSurvey<TSource, TAccumulate>(
      Func<NodeContext<TSource>, TAccumulate> nodeSelector,
      Func<NodeContext<TSource>, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (nodeContext, children) =>
      {
        var accumulate = nodeSelector(nodeContext);
        foreach (var childAccumulate in children)
          accumulate = accumulator(nodeContext, accumulate, childAccumulate);
        return accumulate;
      };
  }
}
