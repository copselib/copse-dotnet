using Copse.Async.Stores;
using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The sibling-complete tier of the leaffix pair: every node gets an accumulation --
    /// leaves from the seed/selector boundary, internal nodes from <paramref name="survey"/>,
    /// which sees ALL of its children at once through the no-copy
    /// <see cref="DispatchSources{TSource, TAccumulate}"/> view: one READ-handle per child
    /// carrying its context and completed accumulation -- <c>DispatchTargets</c>' dual
    /// (docs/SCANRESULT_DESIGN.md), with the same honestly-O(1) Count and indexer off the
    /// builds' shared child-index. Sibling-complete visibility is the point -- median of
    /// children, top-k, anything that must compare children to each other; a fold that only
    /// needs one child at a time belongs to LeaffixScan (sugar over this operator).
    ///
    /// <para>VALUE-flavored (2026-08-02, the ScanResult sweep): the survey receives the node's
    /// VALUE; the leaf boundary is arity-split (seed | value selector | positional
    /// selector). Returns the CANONICAL PAIRING: a buffer of
    /// <see cref="ScanResult{TSource, TAccumulate}"/>s in the source tree's shape -- it
    /// DECORATES rather than replaces; project <c>.Accumulate</c> for values, or use
    /// LeaffixDoDispatch for mutable nodes.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because the pass
    /// MANUFACTURES owned O(n) storage: a root's accumulation IS its whole subtree's
    /// aggregate, so the source is fully consumed before the first result visit can be
    /// published. Deferred: construction is pinned to the first treenumerator acquisition
    /// (Tree.Lazy), and the awaited build runs ONCE. The source is consumed depth-first only,
    /// so a streamed narrow source can leaffix. Build shape (the ScanResult sweep): one raw
    /// capture, the shared child-index, then a reverse-preorder fold -- the same passes as the
    /// rootfix dispatch build, genuinely shared.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, (TSource _, NodePosition __) => seed, survey);

    /// <summary>
    /// The per-leaf seeding form: every leaf's accumulation comes from
    /// <paramref name="leafNodeSelector"/> -- the fringe answers for itself, mirroring
    /// rootfix's rootNodeSelector at the other end of the tree.
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, (TSource node, NodePosition _) => leafNodeSelector(node), survey);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the leaf's value and its position.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        AsyncTree.Lazy(() => PreorderDispatch(source, leafNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: a leaffix fold runs in depth-first subtree-close
    /// order, which a level-order arrival cannot provide, so the source is captured (the same
    /// O(n) every LeaffixDispatch pays, disclosed by the buffer return type) and the pass runs
    /// over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, (TSource _, NodePosition __) => seed, survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, (TSource node, NodePosition _) => leafNodeSelector(node), survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>>(
        AsyncTree.Lazy(() => PreorderDispatchBreadthFirstSource(source, leafNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TAccumulate>> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, survey);

    // Preorder for BOTH dimensions, deliberately: pinning a level-order layout on a
    // breadth-first-first pull was built and MEASURED OUT -- over raw array stores the
    // breadth-first cross-decode tax is only ~1.08x, so the transpose plus transient double
    // storage needs ~5 replays to break even and taxes the common single-drain case ~8%.
    private static IAsyncTreenumerable<ScanResult<TSource, TAccumulate>> PreorderDispatch<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
    {
      var surveyed = new AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        () => BuildLeaffixDispatchAsync(source, leafNodeSelector, survey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(surveyed);
    }

    private static IAsyncTreenumerable<ScanResult<TSource, TAccumulate>> PreorderDispatchBreadthFirstSource<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
    {
      var surveyed = new AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        () => BuildLeaffixDispatchFromBreadthFirstAsync(source, leafNodeSelector, survey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(surveyed);
    }

    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> BuildLeaffixDispatchFromBreadthFirstAsync<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildLeaffixDispatchAsync(capture, leafNodeSelector, survey).ConfigureAwait(false);
    }

    // The pure finisher over the shared fold pass: zip (values, accumulations) into the
    // canonical pairing. The Do finisher (AsyncTreenumerable.LeaffixDoDispatch.cs) rides the
    // same pass and hands the same pairs to its store instead -- one build, two exits, the
    // rootfix pair's arrangement mirrored.
    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> BuildLeaffixDispatchAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
    {
      var (values, subtreeSizes, accumulations) =
        await RunLeaffixDispatchPassAsync(source, leafNodeSelector, survey).ConfigureAwait(false);

      var results = new ScanResult<TSource, TAccumulate>[values.Length];
      for (var nodeIndex = 0; nodeIndex < results.Length; nodeIndex++)
        results[nodeIndex] = new ScanResult<TSource, TAccumulate>(values[nodeIndex], accumulations[nodeIndex]);

      return new AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>(results, subtreeSizes);
    }

    // The shared fold pass, both operators' engine (the ScanResult sweep's rebuild): one raw
    // capture into the flat pre-order encoding, the shared child-index, then a REVERSE-preorder
    // fold -- descendants sit after their parent in preorder, so the backward walk completes
    // every child before its parent's survey runs. The same passes as the rootfix dispatch
    // build; only the fold direction differs.
    private static async ValueTask<(TSource[] Values, int[] SubtreeSizes, TAccumulate[] Accumulations)> RunLeaffixDispatchPassAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
    {
      var (values, subtreeSizes, positions) = await AsyncPreorderCapture
        .CaptureRawAsync(source, nodeContext => nodeContext.Position)
        .ConfigureAwait(false);

      var (childOffsets, childIndices) = DispatchChildIndex.Build(subtreeSizes);

      var accumulations = new TAccumulate[values.Length];
      for (var nodeIndex = values.Length - 1; nodeIndex >= 0; nodeIndex--)
        accumulations[nodeIndex] =
          subtreeSizes[nodeIndex] == 1
          ? leafNodeSelector(values[nodeIndex], positions[nodeIndex])
          : survey(values[nodeIndex], new DispatchSources<TSource, TAccumulate>(values, positions, childIndices, childOffsets, accumulations, nodeIndex));

      return (values, subtreeSizes, accumulations);
    }
  }
}
