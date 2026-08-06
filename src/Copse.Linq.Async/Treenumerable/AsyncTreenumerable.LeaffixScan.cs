using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The fold tier of the leaffix pair -- RootfixScan's TRUE DUAL (reshaped 2026-08-05,
    /// docs/SCANRESULT_DESIGN.md THE NORTH STAR): flow reversal flips the upstream
    /// multiplicity (one parent down, n children up), so the upward fold decomposes into two
    /// callbacks -- <paramref name="edgeAccumulator"/> reduces the children's COMPLETED
    /// accumulations in sibling order (left-fold from the first child, firing k-1 times, so
    /// non-commutative reductions are well-defined and no identity element is demanded), and
    /// <paramref name="nodeAccumulator"/> then folds the node itself in ONCE:
    /// <c>value(n) = nodeAccumulator(edgeReduce(children), n)</c>. The node accumulator is
    /// LITERALLY RootfixScan's fold shape, <c>(TAccumulate, TSource)</c> -- the same fold,
    /// fed by the parent's accumulate going down and by the children's reduced accumulate
    /// going up. (The former map-then-combine shape fused the boundary INTO the map -- "both
    /// an accumulator and a generator" -- and was replaced by this honest decomposition.)
    ///
    /// <para>THE BOUNDARY: selector flavors only -- <paramref name="leafNodeSelector"/> sets
    /// each leaf's accumulation directly, the node accumulator bypassed at the fringe. There
    /// is NO seed flavor at the leaffix boundary, either tier (THE VIRTUAL-ROOT RULE,
    /// 2026-08-06, docs/SCANRESULT_DESIGN.md): a seed is the arrival from a boundary's
    /// virtual node, and only the rootfix boundary has one -- the virtual forest root is a
    /// single tree-lawful node, while a singular virtual node below all leaves would need n
    /// parents, which is no tree. The fringe's honest instrument is the per-leaf rule; a
    /// formula-shaped fringe ("every leaf starts from x, folded") is written
    /// <c>leaf =&gt; nodeAccumulator(x, leaf)</c>. Anything needing all children at once
    /// (median, top-k) is a survey: LeaffixDispatch, the sibling-complete tier this operator
    /// is sugar over -- <c>LeaffixScan(boundary, edge, node)</c> IS the fold-encoded
    /// LeaffixDispatch (CrossTierCoherenceTests).</para>
    ///
    /// <para>Returns the CANONICAL PAIRING: a buffer of
    /// <see cref="NodeAccumulation{TSource, TAccumulate}"/>s -- project <c>.Accumulate</c> for
    /// values; for mutable nodes, land with the composed effect idiom (see LeaffixDispatch's
    /// doc). Callbacks run during the deferred build; only the sibling reduction order is
    /// specified, so callbacks should be pure.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because a leaffix scan
    /// MANUFACTURES owned O(n) storage: a root's accumulation IS its whole subtree's
    /// aggregate, so the source is fully consumed before the first result visit can be
    /// published. Deferred: construction is pinned to the first treenumerator acquisition.
    /// The source is consumed depth-first only, so a streamed narrow source can leaffix.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the leaf's value and its position.</summary>
    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>The breadth-first-only source overload; the disclosure-rule escalation is LeaffixDispatch's.</summary>
    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, edgeAccumulator, nodeAccumulator);

    public static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, edgeAccumulator, nodeAccumulator);

    // The dual fold expressed as a survey -- internal families only (Count >= 1; the leaf
    // boundary is the dispatch flavor's wrapper): reduce the children's completed values in
    // sibling order from the first child, then fold the node in once. This is the whole
    // delegation -- the scan owns no build; LeaffixDispatch's is the one buffer-producing
    // leaffix build.
    private static Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> DualFoldSurvey<TSource, TAccumulate>(
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => (node, children) => nodeAccumulator(EdgeReduce(children, edgeAccumulator), node);

    // Left-fold of the children's completed accumulations, first child as the start -- k-1
    // edge applications, no identity element demanded (internal families always have a child).
    private static TAccumulate EdgeReduce<TSource, TAccumulate>(
      DispatchSources<TSource, TAccumulate> children,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator)
    {
      var reduced = children[0].Accumulate;

      for (var siblingIndex = 1; siblingIndex < children.Count; siblingIndex++)
        reduced = edgeAccumulator(reduced, children[siblingIndex].Accumulate);

      return reduced;
    }
  }
}
