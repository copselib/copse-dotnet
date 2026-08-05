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
    /// well-defined. Anything needing all children at once (median, top-k) or boundary-only
    /// contributions (leaf count) belongs to LeaffixDispatch, the sibling-complete tier this
    /// operator is sugar over.
    ///
    /// <para>VALUE-flavored (2026-08-02, the ScanResult sweep), and returns the CANONICAL
    /// PAIRING: a buffer of <see cref="ScanResult{TSource, TAccumulate}"/>s -- project
    /// <c>.Accumulate</c> for values; for mutable nodes, land with the composed effect idiom
    /// (see LeaffixDispatch's doc -- the demotion record). Callbacks run
    /// during the deferred build, once per node (the selector) and once per child edge (the
    /// accumulator); only the sibling fold order is specified, so callbacks should be pure.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because a leaffix scan
    /// MANUFACTURES owned O(n) storage: a root's accumulation IS its whole subtree's
    /// aggregate, so the source is fully consumed before the first result visit can be
    /// published. Deferred: construction is pinned to the first treenumerator acquisition.
    /// The source is consumed depth-first only, so a streamed narrow source can leaffix.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, FoldSurvey(nodeSelector, accumulator));

    /// <summary>
    /// The context flavor: the combine also sees the FOLDING node's value, for rules like
    /// weighting each child's rollup by the folding node's own factor. If the rule needs more
    /// than that -- other children, child identity -- it is a survey: use LeaffixDispatch.
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, FoldSurvey(nodeSelector, accumulator));

    /// <summary>The breadth-first-only source overload; the disclosure-rule escalation is LeaffixDispatch's.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, FoldSurvey(nodeSelector, accumulator));

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixDispatch(source, FoldSurvey(nodeSelector, accumulator));

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator);

    // The fold expressed as a survey: start at the node's own projection, combine each child's
    // completed accumulation in sibling order (the view enumerates children left-to-right).
    // This is the whole delegation -- the scan owns no build; LeaffixDispatch's is the one
    // buffer-producing leaffix build.
    private static Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> FoldSurvey<TSource, TAccumulate>(
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (node, children) =>
      {
        var accumulate = nodeSelector(node);
        foreach (var child in children)
          accumulate = accumulator(accumulate, child.Accumulate);
        return accumulate;
      };

    private static Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> FoldSurvey<TSource, TAccumulate>(
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (node, children) =>
      {
        var accumulate = nodeSelector(node);
        foreach (var child in children)
          accumulate = accumulator(node, accumulate, child.Accumulate);
        return accumulate;
      };
  }
}
