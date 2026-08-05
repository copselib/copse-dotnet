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
    /// The sibling-complete tier of the leaffix pair: every node's accumulation comes from
    /// <paramref name="survey"/>, which sees the node together with ALL of its children at
    /// once through the no-copy <see cref="DispatchSources{TSource, TAccumulate}"/> view: one
    /// READ-handle per child carrying its context and completed accumulation --
    /// <c>DispatchTargets</c>' dual (docs/SCANRESULT_DESIGN.md), with the same honestly-O(1)
    /// Count and indexer off the builds' shared child-index. Sibling-complete visibility is
    /// the point -- median of children, top-k, anything that must compare children to each
    /// other; a fold that only needs one child at a time belongs to LeaffixScan (sugar over
    /// this operator).
    ///
    /// <para>FULL PARTICIPATION (ratified 2026-08-04 -- boundary-shape-follows-tier-shape):
    /// leaves are not a special case -- the internal pass surveys EVERY node, a leaf's sources
    /// view simply EMPTY. The public surface leads with the leafNodeSelector flavors (a
    /// survey-only overload existed briefly and was DELETED 2026-08-05: the family's one
    /// fixer-less signature -- TAccumulate appears only inside the lambda, so inference
    /// always failed, the type-fixer-first grammar enforced by the compiler itself -- and the
    /// use-case survey showed the sibling-comparative workloads this tier exists for need a
    /// leaf rule anyway, while formula-shaped fringes belong to LeaffixScan's dual fold).
    /// The selector wraps the survey with a leaf branch
    /// (<c>sources.Count == 0 ? boundary : survey</c>).</para>
    ///
    /// <para>THERE IS NO SEED FLAVOR HERE (THE NORTH STAR, 2026-08-05): a SEED is the value
    /// that PARTICIPATES through the tier's callback -- the virtual root's arrival, folded or
    /// surveyed -- and upward flow has no pre-fringe channel for one to enter through; the
    /// leaffix survey has no arrival seat. The old broadcast-seed overload was the bypass
    /// instrument wearing the seed's name (identically <c>_ =&gt; x</c>, the constant
    /// selector) and was deleted so the boundary grammar reads the same on every tier: a seed
    /// exists only where the flow has an entry channel for it; where values are set directly,
    /// the instrument is a SELECTOR.</para>
    ///
    /// <para>VALUE-flavored (2026-08-02, the ScanResult sweep): the survey receives the node's
    /// VALUE; the leaf boundary is arity-split (seed | value selector | positional
    /// selector). Returns the CANONICAL PAIRING: a buffer of
    /// <see cref="ScanResult{TSource, TAccumulate}"/>s in the source tree's shape -- it
    /// DECORATES rather than replaces; project <c>.Accumulate</c> for values. For mutable
    /// nodes, LAND the accumulations with the composed effect idiom -- <c>.Do(visit =&gt; {
    /// if (visit.Mode == TreenumeratorMode.SchedulingNode) visit.Node.Node.Total =
    /// visit.Node.Accumulate; }).Select(pairing =&gt; pairing.Node)</c> -- effects fire per
    /// drain (the re-enumeration contract); Materialize/Memoize is the consumer's pin
    /// (docs/SCANRESULT_DESIGN.md, the demotion record).</para>
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
    /// <summary>
    /// The per-leaf seeding form: sugar wrapping <paramref name="survey"/> with a leaf branch --
    /// every leaf's accumulation comes from <paramref name="leafNodeSelector"/>, the fringe
    /// answering for itself, mirroring rootfix's rootNodeSelector at the other end of the tree.
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
        AsyncTree.Lazy(() => PreorderDispatch(source, LeafBoundedSurvey(leafNodeSelector, survey))), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: a leaffix fold runs in depth-first subtree-close
    /// order, which a level-order arrival cannot provide, so the source is captured (the same
    /// O(n) every LeaffixDispatch pays, disclosed by the buffer return type) and the pass runs
    /// over the capture's depth-first replay.
    /// </summary>
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
        AsyncTree.Lazy(() => PreorderDispatchBreadthFirstSource(source, LeafBoundedSurvey(leafNodeSelector, survey))), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the historical depth-first consumption.</summary>
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

    // The no-leaf-branch adapter onto the unified per-node callback -- the survey answers for
    // every node, fringe included (empty sources). Internal only (the public survey-only
    // overload died 2026-08-05, fixer-less); LeaffixScan's seed flavor rides this path.
    private static Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> FullSurvey<TSource, TAccumulate>(
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => (node, _, sources) => survey(node, sources);

    // The boundary flavors' adapter: the in-band leaf branch. sources.Count == 0 IS the leaf
    // test -- the same fact the old pass read off the subtree sizes, now speaking the view's
    // own vocabulary.
    private static Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> LeafBoundedSurvey<TSource, TAccumulate>(
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey)
      => (node, position, sources) =>
        sources.Count == 0
        ? leafNodeSelector(node, position)
        : survey(node, sources);

    // Preorder for BOTH dimensions, deliberately: pinning a level-order layout on a
    // breadth-first-first pull was built and MEASURED OUT -- over raw array stores the
    // breadth-first cross-decode tax is only ~1.08x, so the transpose plus transient double
    // storage needs ~5 replays to break even and taxes the common single-drain case ~8%.
    private static IAsyncTreenumerable<ScanResult<TSource, TAccumulate>> PreorderDispatch<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> nodeSurvey)
    {
      var surveyed = new AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        () => BuildLeaffixDispatchAsync(source, nodeSurvey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(surveyed);
    }

    private static IAsyncTreenumerable<ScanResult<TSource, TAccumulate>> PreorderDispatchBreadthFirstSource<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> nodeSurvey)
    {
      var surveyed = new AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>(
        () => BuildLeaffixDispatchFromBreadthFirstAsync(source, nodeSurvey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TAccumulate>, AsyncLazyPreorderStore<ScanResult<TSource, TAccumulate>>>(surveyed);
    }

    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> BuildLeaffixDispatchFromBreadthFirstAsync<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> nodeSurvey)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildLeaffixDispatchAsync(capture, nodeSurvey).ConfigureAwait(false);
    }

    // The finisher over the fold pass: zip (values, accumulations) into the canonical
    // pairing.
    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>> BuildLeaffixDispatchAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> nodeSurvey)
    {
      var (values, subtreeSizes, accumulations) =
        await RunLeaffixDispatchPassAsync(source, nodeSurvey).ConfigureAwait(false);

      var results = new ScanResult<TSource, TAccumulate>[values.Length];
      for (var nodeIndex = 0; nodeIndex < results.Length; nodeIndex++)
        results[nodeIndex] = new ScanResult<TSource, TAccumulate>(values[nodeIndex], accumulations[nodeIndex]);

      return new AsyncPreorderArrayStore<ScanResult<TSource, TAccumulate>>(results, subtreeSizes);
    }

    // The shared fold pass, both operators' engine (the ScanResult sweep's rebuild): one raw
    // capture into the flat pre-order encoding, the shared child-index, then a REVERSE-preorder
    // fold -- descendants sit after their parent in preorder, so the backward walk completes
    // every child before its parent's survey runs. The same passes as the rootfix dispatch
    // build; only the fold direction differs. Full participation (2026-08-04): the survey
    // fires on every node -- a leaf's sources view is empty, not skipped.
    private static async ValueTask<(TSource[] Values, int[] SubtreeSizes, TAccumulate[] Accumulations)> RunLeaffixDispatchPassAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, DispatchSources<TSource, TAccumulate>, TAccumulate> nodeSurvey)
    {
      var (values, subtreeSizes, positions) = await AsyncPreorderCapture
        .CaptureRawAsync(source, nodeContext => nodeContext.Position)
        .ConfigureAwait(false);

      var (childOffsets, childIndices) = DispatchChildIndex.Build(subtreeSizes);

      var accumulations = new TAccumulate[values.Length];
      for (var nodeIndex = values.Length - 1; nodeIndex >= 0; nodeIndex--)
        accumulations[nodeIndex] = nodeSurvey(
          values[nodeIndex],
          positions[nodeIndex],
          new DispatchSources<TSource, TAccumulate>(values, positions, childIndices, childOffsets, accumulations, nodeIndex));

      return (values, subtreeSizes, accumulations);
    }
  }
}
