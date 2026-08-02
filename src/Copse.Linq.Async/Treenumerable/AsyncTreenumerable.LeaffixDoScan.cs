using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The IMPURE leaffix fold (SPIKE, feature/do-scan): LeaffixScan's Do twin -- map-then-combine
    /// up the tree, with each node's completed accumulation landing on the caller's objects via
    /// <paramref name="store"/> and the nodes passing through unchanged. Every node's
    /// accumulation starts at <paramref name="nodeSelector"/> (a leaf is the projection
    /// unchanged) and each child's completed accumulation is combined in by
    /// <paramref name="accumulator"/>, one child at a time in sibling order.
    ///
    /// <para>Purity boundary: <paramref name="nodeSelector"/> and <paramref name="accumulator"/>
    /// are PURE; <paramref name="store"/> is the declared effect point -- EXACTLY once per node
    /// per build, preorder order, the (node, accumulation) pairing. Effect count follows the
    /// laziness class: leaffix folds are CAPTURES (children-first), so effects fire once per
    /// operator instance at the first drain and replays never re-fire -- unlike the streaming
    /// RootfixDoScan's per-drain contract, and for the same rule. Composition barrier like
    /// <c>Do</c>.</para>
    ///
    /// <para>Sugar over <see cref="LeaffixDoDispatch{TSource, TAccumulate}"/>, the pure pair's
    /// own delegation LEGITIMATELY mirrored: both leaffix tiers share one cost class, so the
    /// fold rides the sibling-complete build at the measured wrapper premium and improvements
    /// land once. Anything needing all children at once is a survey -- use LeaffixDoDispatch.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, nodeSelector, DoFoldSurvey(nodeSelector, accumulator), store);

    /// <summary>
    /// The context flavor: the combine also sees the FOLDING node's value, for rules like
    /// weighting each child's rollup by the folding node's own factor. Needs more than that --
    /// other children, child identity -- it is a survey: use LeaffixDoDispatch.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, nodeSelector, DoFoldSurvey(nodeSelector, accumulator), store);

    /// <summary>The breadth-first-only source overload; the disclosure-rule escalation is LeaffixDoDispatch's.</summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, nodeSelector, DoFoldSurvey(nodeSelector, accumulator), store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, nodeSelector, DoFoldSurvey(nodeSelector, accumulator), store);

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator, store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator,
      Action<TSource, TAccumulate> store)
      => LeaffixDoScan((IAsyncDepthFirstTreenumerable<TSource>)source, nodeSelector, accumulator, store);

    // The fold expressed as a survey, value-flavored (the Do tier's grammar): start at the
    // node's own projection, combine each child's completed accumulation in sibling order.
    private static Func<TSource, ChildAccumulations<TAccumulate>, TAccumulate> DoFoldSurvey<TSource, TAccumulate>(
      Func<TSource, TAccumulate> nodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (node, children) =>
      {
        var accumulate = nodeSelector(node);
        foreach (var childAccumulate in children)
          accumulate = accumulator(accumulate, childAccumulate);
        return accumulate;
      };

    private static Func<TSource, ChildAccumulations<TAccumulate>, TAccumulate> DoFoldSurvey<TSource, TAccumulate>(
      Func<TSource, TAccumulate> nodeSelector,
      Func<TSource, TAccumulate, TAccumulate, TAccumulate> accumulator)
      => (node, children) =>
      {
        var accumulate = nodeSelector(node);
        foreach (var childAccumulate in children)
          accumulate = accumulator(node, accumulate, childAccumulate);
        return accumulate;
      };
  }
}
