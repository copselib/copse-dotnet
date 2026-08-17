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
    /// design-docs/SCANRESULT_DESIGN.md THE NORTH STAR): flow reversal flips the upstream
    /// multiplicity (one parent down, n children up), so the upward fold decomposes into two
    /// callbacks -- <paramref name="edgeAccumulator"/> reduces the children's COMPLETED
    /// accumulations in sibling order (left-fold from the first child, firing k-1 times, so
    /// non-commutative reductions are well-defined and no identity element is demanded), and
    /// <paramref name="nodeAccumulator"/> then folds the node itself in ONCE:
    /// <c>value(n) = nodeAccumulator(edgeReduce(children), n)</c>. The node accumulator is
    /// LITERALLY RootfixScan's fold shape, <c>(TAccumulate, TSource)</c> -- the same fold,
    /// fed by the parent's accumulate going down and by the children's reduced accumulate
    /// going up. (The former map-then-combine shape folded the boundary INTO the map itself -- "both
    /// an accumulator and a generator" -- and was replaced by this honest decomposition.)
    ///
    /// <para>THE BOUNDARY: selector flavors only -- <paramref name="leafNodeSelector"/> sets
    /// each leaf's accumulation directly, the node accumulator bypassed at the fringe. There
    /// is NO seed flavor at the leaffix boundary, either tier (THE VIRTUAL-ROOT RULE,
    /// 2026-08-06, design-docs/SCANRESULT_DESIGN.md): a seed is the arrival from a boundary's
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
    {
      // The receiver sniff (the Materialize / LINQ-Count idiom; the 2026-08-14 experiment's
      // collapse): ANY capture folds IN PLACE over its own adjacency -- no second capture,
      // no layout condition (Stage B's stance fold assigns its own preorder numbering, so
      // the receiver's handle space is never assumed; the old preorder-only guard died with
      // the migration). A concrete preorder buffer takes the span fast path inside; every
      // other capture takes the walker fold. True streams take the dispatch delegation.
      // Only the VALUE-selector flavor folds in place (re-keyed from the seed flavor when
      // the virtual-root rule retired it -- the leaf slot is position-free either way): the
      // positional flavor needs per-node positions, which the engine derives and the
      // in-place fold deliberately does not.
      if (source is IAsyncTreenumerableBuffer<TSource> buffer)
        return InPlaceLeaffixScan(buffer, leafNodeSelector, edgeAccumulator, nodeAccumulator);

      return StreamLeaffixScan(source, leafNodeSelector, edgeAccumulator, nodeAccumulator);
    }

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
    {
      // The same sniff on the breadth-first receiver: a capture is a full citizen whatever
      // static type it arrives through (the collapse covered every receiver shape).
      if (source is IAsyncTreenumerableBuffer<TSource> buffer)
        return InPlaceLeaffixScan(buffer, leafNodeSelector, edgeAccumulator, nodeAccumulator);

      // The disclosure-rule escalation, the dispatch tier's shape: a leaffix fold runs in
      // depth-first subtree-close order, which a level-order arrival cannot provide, so the
      // stream is captured once and the pass runs over the capture's depth-first replay.
      return StreamLeaffixScan(source.Materialize(), leafNodeSelector, edgeAccumulator, nodeAccumulator);
    }

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

    // The in-place regime: the fold runs over the receiver's own adjacency at the result's
    // first pull -- the concrete buffer hands its raw store (span arithmetic; no probes, no
    // child-index build, no positions build), a foreign walkable folds through the public
    // probes (a memo's pull-through probes complete it exactly once, with no second
    // skeleton). The result is the scan family's CITIZEN buffer over a SHARED fold pass
    // (SELECT_INTO_CAPTURES_DESIGN.md): the canonical pairing is just the default product,
    // and a composed Select zips a sibling variant from the same pass.
    private static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> InPlaceLeaffixScan<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      var foldPass = new AsyncScanFoldPass<TSource, TAccumulate>(
        fuseCanonicalPairing => BuildInPlaceLeaffixArtifactsAsync(buffer, leafNodeSelector, edgeAccumulator, nodeAccumulator, fuseCanonicalPairing));

      return new AsyncScanProductBuffer<TSource, TAccumulate, NodeAccumulation<TSource, TAccumulate>>(foldPass, PairProduct, isCanonicalPairing: true);
    }

    // The stream regime, same citizen shape: the dispatch fold pass runs once behind the
    // shared pass; the pairing is the default finisher. (LeaffixDispatch's own surface keeps
    // its pair product; only the scan tier is a citizen.)
    private static IAsyncTreenumerableBuffer<NodeAccumulation<TSource, TAccumulate>> StreamLeaffixScan<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator)
    {
      // COMPOSE-LEFT (the door, SELECT_INTO_CAPTURES_DESIGN.md): a pure-projection wrapper
      // upstream surrenders its pieces, and the capture pass walks the UN-projected inner
      // raw -- zero wrapper layers on the walk; the projection becomes one array map inside
      // the pass. The consumer's Consume<TInner> is where the wrapper's hidden inner type
      // gets its name back.
      if (source is IAsyncProjectionSource<TSource> projectionSource)
        return projectionSource.CaptureThrough(
          new AsyncLeaffixFromProjectionConsumer<TSource, TAccumulate>(leafNodeSelector, edgeAccumulator, nodeAccumulator));

      var foldPass = new AsyncScanFoldPass<TSource, TAccumulate>(
        fuseCanonicalPairing => BuildStreamLeaffixArtifactsAsync(source, leafNodeSelector, edgeAccumulator, nodeAccumulator, fuseCanonicalPairing));

      return new AsyncScanProductBuffer<TSource, TAccumulate, NodeAccumulation<TSource, TAccumulate>>(foldPass, PairProduct, isCanonicalPairing: true);
    }

    // The consumer half of the compose-left door: builds the SAME citizen buffer the plain
    // stream path builds, over a fold pass that walks the surrendered inner directly.
    private sealed class AsyncLeaffixFromProjectionConsumer<TProjected, TAccumulate>
      : IAsyncProjectionConsumer<TProjected, IAsyncTreenumerableBuffer<NodeAccumulation<TProjected, TAccumulate>>>
    {
      public AsyncLeaffixFromProjectionConsumer(
        Func<TProjected, TAccumulate> leafNodeSelector,
        Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
        Func<TAccumulate, TProjected, TAccumulate> nodeAccumulator)
      {
        _LeafNodeSelector = leafNodeSelector;
        _EdgeAccumulator = edgeAccumulator;
        _NodeAccumulator = nodeAccumulator;
      }

      private readonly Func<TProjected, TAccumulate> _LeafNodeSelector;
      private readonly Func<TAccumulate, TAccumulate, TAccumulate> _EdgeAccumulator;
      private readonly Func<TAccumulate, TProjected, TAccumulate> _NodeAccumulator;

      public IAsyncTreenumerableBuffer<NodeAccumulation<TProjected, TAccumulate>> Consume<TInner>(
        IAsyncTreenumerable<TInner> innerSource,
        Func<NodeContext<TInner>, TProjected> projector)
      {
        var foldPass = new AsyncScanFoldPass<TProjected, TAccumulate>(
          fuseCanonicalPairing => BuildStreamLeaffixFromProjectionArtifactsAsync(innerSource, projector, _LeafNodeSelector, _EdgeAccumulator, _NodeAccumulator, fuseCanonicalPairing));

        return new AsyncScanProductBuffer<TProjected, TAccumulate, NodeAccumulation<TProjected, TAccumulate>>(foldPass, PairProduct, isCanonicalPairing: true);
      }
    }

    // The fused pass: one RAW capture of the inner (no wrapper on any pull), the child-index
    // build the dispatch pass already runs, ONE linear map through the projector (positions
    // in hand, so the wrapper's full NodeContext selector is honored), then the standard
    // reverse fold over projected values.
    private static async ValueTask<(ScanFoldArtifacts<TProjected, TAccumulate> Artifacts, NodeAccumulation<TProjected, TAccumulate>[] FusedPairProducts)> BuildStreamLeaffixFromProjectionArtifactsAsync<TInner, TProjected, TAccumulate>(
      IAsyncTreenumerable<TInner> innerSource,
      Func<NodeContext<TInner>, TProjected> projector,
      Func<TProjected, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TProjected, TAccumulate> nodeAccumulator,
      bool fuseCanonicalPairing)
    {
      var (innerValues, subtreeSizes) = await AsyncPreorderCapture.CaptureRawAsync(innerSource).ConfigureAwait(false);

      var (childOffsets, childIndices, positions) = DispatchChildIndex.BuildWithPositions(subtreeSizes);

      var values = new TProjected[innerValues.Length];
      for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        values[nodeIndex] = projector(new NodeContext<TInner>(innerValues[nodeIndex], positions[nodeIndex]));

      var survey = LeafBoundedSurvey(
        (TProjected leafValue, NodePosition _) => leafNodeSelector(leafValue),
        DualFoldSurvey(edgeAccumulator, nodeAccumulator));

      var accumulations = new TAccumulate[values.Length];
      for (var nodeIndex = values.Length - 1; nodeIndex >= 0; nodeIndex--)
        accumulations[nodeIndex] = survey(
          values[nodeIndex],
          positions[nodeIndex],
          new DispatchSources<TProjected, TAccumulate>(values, positions, childIndices, childOffsets, accumulations, nodeIndex));

      return (
        new ScanFoldArtifacts<TProjected, TAccumulate>(nodeIndex => values[nodeIndex], accumulations, subtreeSizes),
        fuseCanonicalPairing ? FusePairProducts(values, accumulations) : null);
    }

    private static async ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)> BuildStreamLeaffixArtifactsAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator,
      bool fuseCanonicalPairing)
    {
      var (values, subtreeSizes, accumulations) = await RunLeaffixDispatchPassAsync(
          source,
          LeafBoundedSurvey(
            (TSource leaf, NodePosition _) => leafNodeSelector(leaf),
            DualFoldSurvey(edgeAccumulator, nodeAccumulator)))
        .ConfigureAwait(false);

      return (
        new ScanFoldArtifacts<TSource, TAccumulate>(nodeIndex => values[nodeIndex], accumulations, subtreeSizes),
        fuseCanonicalPairing ? FusePairProducts(values, accumulations) : null);
    }

    // The stream builds' fusion: values and accumulations are hot arrays here, so the pair
    // write is direct reads plus an inline constructor -- no ValueAt delegate on the path.
    private static NodeAccumulation<TSource, TAccumulate>[] FusePairProducts<TSource, TAccumulate>(
      TSource[] values,
      TAccumulate[] accumulations)
    {
      var fusedPairProducts = new NodeAccumulation<TSource, TAccumulate>[values.Length];

      for (var nodeIndex = 0; nodeIndex < fusedPairProducts.Length; nodeIndex++)
        fusedPairProducts[nodeIndex] = new NodeAccumulation<TSource, TAccumulate>(values[nodeIndex], accumulations[nodeIndex]);

      return fusedPairProducts;
    }

    // The product-selector seam (SELECT_INTO_CAPTURES_DESIGN.md): what a scan STORES is
    // productSelector(node, accumulate) -- the canonical pairing by default. A composed
    // Select retargets the selector to f-after-pair: the pair struct is still constructed
    // per node, on the stack, but never stored, so the composed product is 1-wide from
    // birth.
    private static NodeAccumulation<TSource, TAccumulate> PairProduct<TSource, TAccumulate>(TSource node, TAccumulate accumulate)
      => new NodeAccumulation<TSource, TAccumulate>(node, accumulate);

    private static async ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)> BuildInPlaceLeaffixArtifactsAsync<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator,
      bool fuseCanonicalPairing)
    {
      if (buffer is AsyncTreenumerableBuffer<TSource> concreteBuffer)
      {
        var (hasStore, store) = await concreteBuffer.TryGetPreorderStoreAsync().ConfigureAwait(false);

        if (hasStore)
          return SpanLeaffix(store, leafNodeSelector, edgeAccumulator, nodeAccumulator, fuseCanonicalPairing);
      }

      return await WalkerLeaffixAsync(buffer, leafNodeSelector, edgeAccumulator, nodeAccumulator, fuseCanonicalPairing).ConfigureAwait(false);
    }

    // The span fold: reverse-ordinal (children complete before their parent by preorder
    // construction), children found by span hops (first child = handle + 1, next sibling =
    // a subtree-size hop, the node's span end fences the walk). Reads the skeleton the
    // receiver already owns; the store's own facts are the only inputs. The fringe is the
    // selector's, directly (the virtual-root rule: no seed at the leaffix boundary).
    private static (ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts) SpanLeaffix<TSource, TAccumulate>(
      AsyncPreorderArrayStore<TSource> store,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator,
      bool fuseCanonicalPairing)
    {
      var nodeCount = store.Count;

      var accumulates = new TAccumulate[nodeCount];
      var subtreeSizes = new int[nodeCount];
      var fusedPairProducts = fuseCanonicalPairing ? new NodeAccumulation<TSource, TAccumulate>[nodeCount] : null;

      for (var handle = nodeCount - 1; handle >= 0; handle--)
      {
        var subtreeSize = store.GetSubtreeSize(handle);
        var node = store.GetValue(handle);

        TAccumulate accumulate;

        if (subtreeSize > 1)
        {
          var spanEnd = handle + subtreeSize;
          var child = handle + 1;

          var reduced = accumulates[child];

          for (child += store.GetSubtreeSize(child); child < spanEnd; child += store.GetSubtreeSize(child))
            reduced = edgeAccumulator(reduced, accumulates[child]);

          accumulate = nodeAccumulator(reduced, node);
        }
        else
        {
          accumulate = leafNodeSelector(node);
        }

        accumulates[handle] = accumulate;
        subtreeSizes[handle] = subtreeSize;

        // The first-caller fusion: the canonical product written INLINE, node in register --
        // the pre-pass fold's exact cost for the un-composed spelling.
        if (fusedPairProducts != null)
          fusedPairProducts[handle] = new NodeAccumulation<TSource, TAccumulate>(node, accumulate);
      }

      // The value reader is the receiver's own store: an in-place pass never copies values.
      return (new ScanFoldArtifacts<TSource, TAccumulate>(store.GetValue, accumulates, subtreeSizes), fusedPairProducts);
    }

    // The same fold in PURE STANCE VOCABULARY (Stage B's first migration, the receipts
    // methodology's first entry): one depth-first walk of doors + steps + extract, no handle
    // arithmetic, no handle-space enumeration, no re-entry. The walk assigns its own
    // preorder numbering (a node's output index at first visit; its span closed when its
    // frame pops), so the fold makes NO assumption about the receiver's handle space at all
    // -- any walkable capture folds in place, whatever its layout. Receipt for the ledger:
    // ZERO new walker features were needed; door + steps + extract are fold-complete, and
    // ordinal indexing was only ever speed, which the concrete span path already owns.
    private static async ValueTask<(ScanFoldArtifacts<TSource, TAccumulate> Artifacts, NodeAccumulation<TSource, TAccumulate>[] FusedPairProducts)> WalkerLeaffixAsync<TSource, TAccumulate>(
      IAsyncTreenumerableBuffer<TSource> buffer,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TAccumulate, TAccumulate, TAccumulate> edgeAccumulator,
      Func<TAccumulate, TSource, TAccumulate> nodeAccumulator,
      bool fuseCanonicalPairing)
    {
      var values = new List<TSource>();
      var accumulates = new List<TAccumulate>();
      var subtreeSizes = new List<int>();
      var fusedPairProducts = fuseCanonicalPairing ? new List<NodeAccumulation<TSource, TAccumulate>>() : null;
      var frames = new Stack<(AsyncTreeWalker<TSource, int> Walker, TSource Value, int ChildIndex, bool Folded, TAccumulate Reduced, int OutputIndex)>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootStance = await buffer.TryGetTreeWalkerAtRootIndexAsync(rootIndex).ConfigureAwait(false);

        if (!rootStance.HasWalker)
          break;

        frames.Push(await OpenLeaffixFrameAsync(rootStance.Walker, values, accumulates, subtreeSizes, fusedPairProducts).ConfigureAwait(false));

        while (frames.Count > 0)
        {
          var frame = frames.Pop();
          var step = await frame.Walker.MoveToChildAsync(frame.ChildIndex).ConfigureAwait(false);

          if (step.HasWalker)
          {
            frames.Push((frame.Walker, frame.Value, frame.ChildIndex + 1, frame.Folded, frame.Reduced, frame.OutputIndex));
            frames.Push(await OpenLeaffixFrameAsync(step.Walker, values, accumulates, subtreeSizes, fusedPairProducts).ConfigureAwait(false));
            continue;
          }

          // The frame closes: every child's accumulation has folded in. A leaf (nothing
          // folded) takes the selector's value directly -- the virtual-root rule's bypass,
          // the only fringe instrument there is.
          var accumulate = frame.Folded
            ? nodeAccumulator(frame.Reduced, frame.Value)
            : leafNodeSelector(frame.Value);

          accumulates[frame.OutputIndex] = accumulate;
          subtreeSizes[frame.OutputIndex] = values.Count - frame.OutputIndex;

          // The first-caller fusion, the walk's close-time write (the pre-pass fold's shape).
          if (fusedPairProducts != null)
            fusedPairProducts[frame.OutputIndex] = new NodeAccumulation<TSource, TAccumulate>(frame.Value, accumulate);

          if (frames.Count > 0)
          {
            var parent = frames.Pop();
            frames.Push((parent.Walker, parent.Value, parent.ChildIndex, true,
              parent.Folded ? edgeAccumulator(parent.Reduced, accumulate) : accumulate, parent.OutputIndex));
          }
        }
      }

      var valueArray = values.ToArray();

      return (
        new ScanFoldArtifacts<TSource, TAccumulate>(nodeIndex => valueArray[nodeIndex], accumulates.ToArray(), subtreeSizes.ToArray()),
        fusedPairProducts?.ToArray());
    }

    private static async ValueTask<(AsyncTreeWalker<TSource, int> Walker, TSource Value, int ChildIndex, bool Folded, TAccumulate Reduced, int OutputIndex)> OpenLeaffixFrameAsync<TSource, TAccumulate>(
      AsyncTreeWalker<TSource, int> walker,
      List<TSource> values,
      List<TAccumulate> accumulates,
      List<int> subtreeSizes,
      List<NodeAccumulation<TSource, TAccumulate>> fusedPairProducts)
    {
      var outputIndex = values.Count;
      var value = await walker.GetValueAsync().ConfigureAwait(false);

      values.Add(value);
      accumulates.Add(default);
      subtreeSizes.Add(0);
      fusedPairProducts?.Add(default);

      return (walker, value, 0, false, default, outputIndex);
    }

    // The dual fold expressed as a survey -- internal families only (Count >= 1; the leaf
    // boundary is the dispatch flavor's wrapper): reduce the children's completed values in
    // sibling order from the first child, then fold the node in once. This is the whole
    // delegation -- the scan owns no build; LeaffixDispatch's is the one buffer-producing
    // leaffix build (and the in-place fold above is its receiver-smart twin, pinned
    // coherent by the conformance battery).
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
