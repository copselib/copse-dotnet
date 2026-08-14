using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerables;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    /// <para>THE BOUNDARY, both instruments (mirroring rootfix exactly): the
    /// <paramref name="seed"/> is what arrives at a LEAF from below -- the VIRTUAL FRINGE's
    /// arrival, the virtual forest root's dual -- and it PARTICIPATES through the fold:
    /// <c>value(leaf) = nodeAccumulator(seed, leaf)</c>, character-for-character the dual of
    /// <c>fold(seed, root)</c>. The leafNodeSelector flavors are the BYPASS instrument: each
    /// leaf's accumulation set directly, the node accumulator skipped at the fringe. Seed and
    /// constant selector are therefore deliberately DIFFERENT -- the same two-instruments pin
    /// as every other boundary in the family. Anything needing all children at once (median,
    /// top-k) is a survey: LeaffixDispatch, the sibling-complete tier this operator is sugar
    /// over -- <c>LeaffixScan(boundary, edge, node)</c> IS the fold-encoded LeaffixDispatch
    /// (CrossTierCoherenceTests).</para>
    ///
    /// <para>Returns the CANONICAL PAIRING: a buffer of
    /// <see cref="ScanResult{TSource, TAccumulate}"/>s -- project <c>.Accumulate</c> for
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
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      // The receiver sniff (the Materialize / LINQ-Count idiom; the 2026-08-14 experiment's
      // collapse): ANY capture folds IN PLACE over its own adjacency -- no second capture,
      // no layout condition (Stage B's stance fold assigns its own preorder numbering, so
      // the receiver's handle space is never assumed; the old preorder-only guard died with
      // the migration). A concrete preorder buffer takes the span fast path inside; every
      // other capture takes the walker fold. True streams take the engine. Only the seed
      // flavor folds in place: the bypass/positional flavors need per-node positions, which
      // the engine derives and the in-place fold deliberately does not.
      if (source is IAsyncTreenumerableBuffer<TSource> buffer)
        return InPlaceLeaffixScan(buffer, seed, edgeAccumulator, nodeAccumulator);

      return new AsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        AsyncTree.Lazy(() => PreorderDispatch(source, FullSurvey(SeededDualFoldSurvey(seed, edgeAccumulator, nodeAccumulator)))), BufferLayout.Preorder);
    }

    // The in-place regime: the fold runs over the receiver's own adjacency at the result's
    // first pull -- the concrete buffer hands its raw store (span arithmetic; no probes, no
    // child-index build, no positions build), a foreign walkable folds through the public
    // probes (a memo's pull-through probes complete it exactly once, with no second
    // skeleton). The result buffer gets its probes at birth, sharing the one lazy store.
    private static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> InPlaceLeaffixScan<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var scanned = new AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        () => BuildInPlaceLeaffixAsync(buffer, seed, edgeAccumulator, nodeAccumulator));

      return new AsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        new AsyncPreorderTreenumerable<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(scanned),
        BufferLayout.Preorder,
        new AsyncPreorderAdjacencyIndex<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(scanned));
    }

    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> BuildInPlaceLeaffixAsync<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      if (buffer is AsyncTreenumerableBuffer<TSource> concreteBuffer)
      {
        var (hasStore, store) = await concreteBuffer.TryGetPreorderStoreAsync().ConfigureAwait(false);

        if (hasStore)
          return SpanLeaffix(store, seed, edgeAccumulator, nodeAccumulator);
      }

      return await WalkerLeaffixAsync(buffer, seed, edgeAccumulator, nodeAccumulator).ConfigureAwait(false);
    }

    // The span fold: reverse-ordinal (children complete before their parent by preorder
    // construction), children found by span hops (first child = handle + 1, next sibling =
    // a subtree-size hop, the node's span end fences the walk). Reads the skeleton the
    // receiver already owns; the store's own facts are the only inputs.
    private static AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>> SpanLeaffix<TSource, TAccumulate>(
      AsyncPreorderArrayStore<TSource> store,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var nodeCount = store.Count;

      var accumulates = new TAccumulate[nodeCount];
      var subtreeSizes = new int[nodeCount];
      var results = new ScanResult<TSource, TAccumulate>[nodeCount];

      for (var handle = nodeCount - 1; handle >= 0; handle--)
      {
        var subtreeSize = store.GetSubtreeSize(handle);
        var reduced = seed;

        if (subtreeSize > 1)
        {
          var spanEnd = handle + subtreeSize;
          var child = handle + 1;

          reduced = accumulates[child];

          for (child += store.GetSubtreeSize(child); child < spanEnd; child += store.GetSubtreeSize(child))
            reduced = edgeAccumulator(reduced, accumulates[child]);
        }

        var node = store.GetValue(handle);

        accumulates[handle] = nodeAccumulator(reduced, node);
        subtreeSizes[handle] = subtreeSize;
        results[handle] = new ScanResult<TSource, TAccumulate>(node, accumulates[handle]);
      }

      return new AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>(results, subtreeSizes);
    }

    // The same fold in PURE STANCE VOCABULARY (Stage B's first migration, the receipts
    // methodology's first entry): one depth-first walk of doors + steps + extract, no handle
    // arithmetic, no handle-space enumeration, no re-entry. The walk assigns its own
    // preorder numbering (a node's output index at first visit; its span closed when its
    // frame pops), so the fold makes NO assumption about the receiver's handle space at all
    // -- any walkable capture folds in place, whatever its layout. Receipt for the ledger:
    // ZERO new walker features were needed; door + steps + extract are fold-complete, and
    // ordinal indexing was only ever speed, which the concrete span path already owns.
    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> WalkerLeaffixAsync<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var results = new List<ScanResult<TSource, TAccumulate>>();
      var subtreeSizes = new List<int>();
      var frames = new Stack<(AsyncTreeWalker<TSource, int> Walker, TSource Value, int ChildIndex, bool Folded, TAccumulate Reduced, int OutputIndex)>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootStance = await buffer.TryGetTreeWalkerAtRootIndexAsync(rootIndex).ConfigureAwait(false);

        if (!rootStance.HasWalker)
          break;

        frames.Push(await OpenLeaffixFrameAsync(rootStance.Walker, results, subtreeSizes).ConfigureAwait(false));

        while (frames.Count > 0)
        {
          var frame = frames.Pop();
          var step = await frame.Walker.MoveToChildAsync(frame.ChildIndex).ConfigureAwait(false);

          if (step.HasWalker)
          {
            frames.Push((frame.Walker, frame.Value, frame.ChildIndex + 1, frame.Folded, frame.Reduced, frame.OutputIndex));
            frames.Push(await OpenLeaffixFrameAsync(step.Walker, results, subtreeSizes).ConfigureAwait(false));
            continue;
          }

          // The frame closes: every child's accumulation has folded in; the seed stands in
          // at the fringe (the virtual fringe's arrival, participating through the fold).
          var accumulate = nodeAccumulator(frame.Folded ? frame.Reduced : seed, frame.Value);

          results[frame.OutputIndex] = new ScanResult<TSource, TAccumulate>(frame.Value, accumulate);
          subtreeSizes[frame.OutputIndex] = results.Count - frame.OutputIndex;

          if (frames.Count > 0)
          {
            var parent = frames.Pop();
            frames.Push((parent.Walker, parent.Value, parent.ChildIndex, true,
              parent.Folded ? edgeAccumulator(parent.Reduced, accumulate) : accumulate, parent.OutputIndex));
          }
        }
      }

      return new AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>(results.ToArray(), subtreeSizes.ToArray());
    }

    private static async ValueTask<(AsyncTreeWalker<TSource, int> Walker, TSource Value, int ChildIndex, bool Folded, TAccumulate Reduced, int OutputIndex)> OpenLeaffixFrameAsync<TSource, TAccumulate>(
      AsyncTreeWalker<TSource, int> walker,
      List<ScanResult<TSource, TAccumulate>> results,
      List<int> subtreeSizes)
    {
      var outputIndex = results.Count;

      results.Add(default);
      subtreeSizes.Add(0);

      return (walker, await walker.GetValueAsync().ConfigureAwait(false), 0, false, default, outputIndex);
    }

    /// <summary>
    /// The per-leaf flavor -- the BYPASS instrument: every leaf's accumulation comes from
    /// <paramref name="leafNodeSelector"/> DIRECTLY, the node accumulator skipped at the
    /// fringe (set each leaf explicitly; the seed flavor is the other instrument -- the
    /// virtual fringe's arrival, folded).
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the leaf's value and its position.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>The breadth-first-only source overload; the disclosure-rule escalation is LeaffixDispatch's.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        AsyncTree.Lazy(() => PreorderDispatchBreadthFirstSource(source, FullSurvey(SeededDualFoldSurvey(seed, edgeAccumulator, nodeAccumulator)))), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixDispatch(source, leafNodeSelector, DualFoldSurvey(edgeAccumulator, nodeAccumulator));

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, seed, edgeAccumulator, nodeAccumulator);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => LeaffixScan((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, edgeAccumulator, nodeAccumulator);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixScan<TSource, TAccumulate>(
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

    // The seed flavor's survey -- full participation, no leaf branch outside it: a leaf's
    // reduction is EMPTY, so the virtual fringe's arrival stands in and the node accumulator
    // folds every node, fringe included.
    private static Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> SeededDualFoldSurvey<TSource, TAccumulate>(
      TAccumulate seed,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
      => (node, children) => nodeAccumulator(children.Count == 0 ? seed : EdgeReduce(children, edgeAccumulator), node);

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
