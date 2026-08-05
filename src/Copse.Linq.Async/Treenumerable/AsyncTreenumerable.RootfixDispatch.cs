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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The survey-shaped downward pass -- the sibling-complete tier of the rootfix pair (the
    /// fold-shaped tier is RootfixScan): ONE dispatcher for every family in the forest.
    /// <paramref name="survey"/> receives a family's arrival together with ALL of that
    /// family's members at once through the no-copy
    /// <see cref="DispatchTargets{TSource, TDispatch}"/> view -- one write-handle per member,
    /// each of which must receive exactly one
    /// <see cref="DispatchTarget{TSource, TDispatch}.Dispatch"/> (a second throws immediately;
    /// a missed one throws when the survey returns). Sibling-complete visibility is the point:
    /// a fairness split cannot allocate its edges independently, and a setter-callback
    /// allocator plugs in verbatim -- <c>(child, amount) =&gt; child.Dispatch(amount)</c> IS
    /// its assignment callback. Surveys run in depth-first preorder.
    ///
    /// <para>FULL PARTICIPATION (2026-08-04; unified same day -- the boundary is an
    /// INVOCATION, not a callback): the forest's roots are the children of the VIRTUAL FOREST
    /// ROOT (<see cref="NodePosition.ForestRoot"/>, the machinery's standing convention), and
    /// that family goes first through the SAME survey: <c>(seed, roots)</c>, then
    /// <c>(arrival, children)</c> at every internal node. No node class sits outside the
    /// dispatcher, no root-specific callback exists, and a budget allocates ACROSS the roots
    /// exactly the way it allocates across any other family. The rootNodeSelector flavors are
    /// the boundary's sugar for roots that follow a different, per-root rule.</para>
    ///
    /// <para>THE SUBJECT SEAT, REMOVED (2026-08-04 -- the seat rule, aimed at the survey): the
    /// surveyed family's parent VALUE is derivable, so it holds no seat. A node's arrival is
    /// authored at its parent's dispatch site, where that node is in hand as the target's
    /// <c>.Node</c> -- any subject-shaped fact a survey needs, the caller flows INSIDE
    /// <typeparamref name="TDispatch"/> at the moment of dispatch. (Contrast LeaffixDispatch,
    /// whose survey keeps its subject: upward flow means the node's own value passes through
    /// nobody else's hands -- each survey keeps exactly the seats its flow direction cannot
    /// derive.)</para>
    ///
    /// <para>The result pairs every source value with what ARRIVED at it
    /// (<see cref="ScanResult{TSource, TDispatch}"/>, the family's canonical pairing --
    /// docs/SCANRESULT_DESIGN.md) in the source tree's shape. NOTE the deliberate contrast
    /// with the fold tiers: a fold records its OUTPUT, while this survey records its INPUT --
    /// a node's pairing is what its family's survey dispatched to it -- because the survey's
    /// outputs are edge-grained and land as the MEMBERS' arrivals; a survey has no
    /// node-grained output to record. Project <c>.Accumulate</c> away with Select for
    /// immutable values. For mutable nodes, LAND the arrivals with the composed effect idiom
    /// -- <c>.Do(visit =&gt; { if (visit.Mode == TreenumeratorMode.SchedulingNode)
    /// visit.Node.Node.Amount = visit.Node.Accumulate; }).Select(pairing =&gt;
    /// pairing.Node)</c> -- effects fire per drain (the re-enumeration contract);
    /// Materialize/Memoize is the consumer's pin (docs/SCANRESULT_DESIGN.md, the demotion
    /// record).</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> for LeaffixDispatch's
    /// reason, mirrored: the survey needs its FULL member list before the first member's value
    /// exists, and in a depth-first stream a parent's children are separated by entire sibling
    /// subtrees -- so the source is fully consumed before the first result visit can be
    /// published. Deferred: construction is pinned to the first treenumerator acquisition
    /// (Tree.Lazy), and the awaited build runs ONCE, on the first replay pull. The source is
    /// consumed depth-first only, so a streamed narrow source can dispatch.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatch(source, targets => survey(seed, targets), survey)), BufferLayout.Preorder);

    /// <summary>
    /// The per-root seeding flavor -- A DIFFERENT INSTRUMENT than the seed flavor, not a
    /// different spelling of it (ruled 2026-08-04): every root's arrival comes from
    /// <paramref name="rootNodeSelector"/> DIRECTLY, bypassing the survey -- set each root's
    /// seed explicitly (known per-root budgets) -- where the seed flavor hands ONE value to
    /// your survey at the virtual family to divide among the roots (one budget, divvied).
    /// Consequently <c>RootfixDispatch(seed, survey)</c> is NOT
    /// <c>RootfixDispatch(_ =&gt; seed, survey)</c> here, though the two coincide on the fold
    /// tier: a fold transforms both flavors identically (arrival != value), while on this
    /// tier arrival IS the value and the virtual-family survey is the only transformation
    /// point, which the selector bypasses. Pinned deliberately-different by
    /// RootfixDispatchTests.
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatch(source, PerRootSurvey<TSource, TDispatch>((node, _) => rootNodeSelector(node)), survey)), BufferLayout.Preorder);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatch(source, PerRootSurvey<TSource, TDispatch>(rootNodeSelector), survey)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: the build's structure pass runs depth-first, which a
    /// level-order arrival cannot provide, so the source is captured (the same O(n) every
    /// RootfixDispatch pays, disclosed by the buffer return type) and the pass runs over the
    /// capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatchBreadthFirstSource(source, targets => survey(seed, targets), survey)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatchBreadthFirstSource(source, PerRootSurvey<TSource, TDispatch>((node, _) => rootNodeSelector(node)), survey)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatchBreadthFirstSource(source, PerRootSurvey<TSource, TDispatch>(rootNodeSelector), survey)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      TDispatch seed,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey);

    public static IAsyncTreenumerableBuffer<ScanResult<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey);

    // The selector flavors' boundary: per-root dispatch in isolation -- each root's arrival
    // computed from its own context, no sibling-complete visibility (under the seed flavor the
    // roots are simply the survey's first family, sibling-complete like every other).
    private static Action<DispatchTargets<TSource, TDispatch>> PerRootSurvey<TSource, TDispatch>(
      Func<TSource, NodePosition, TDispatch> rootNodeSelector)
      => targets =>
      {
        foreach (var target in targets)
          target.Dispatch(rootNodeSelector(target.Node, target.Context.Position));
      };

    // Preorder for BOTH dimensions, matching LeaffixDispatch's measured layout decision (see its
    // note: the breadth-first cross-decode tax over raw array stores is ~1.08x, not worth a
    // transpose).
    private static IAsyncTreenumerable<ScanResult<TSource, TDispatch>> PreorderRootfixDispatch<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var dispatched = new AsyncLazyPreorderStore<ScanResult<TSource, TDispatch>>(
        () => BuildRootfixDispatchAsync(source, rootFamilySurvey, survey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TDispatch>, AsyncLazyPreorderStore<ScanResult<TSource, TDispatch>>>(dispatched);
    }

    private static IAsyncTreenumerable<ScanResult<TSource, TDispatch>> PreorderRootfixDispatchBreadthFirstSource<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var dispatched = new AsyncLazyPreorderStore<ScanResult<TSource, TDispatch>>(
        () => BuildRootfixDispatchFromBreadthFirstAsync(source, rootFamilySurvey, survey));

      return new AsyncPreorderTreenumerable<ScanResult<TSource, TDispatch>, AsyncLazyPreorderStore<ScanResult<TSource, TDispatch>>>(dispatched);
    }

    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TDispatch>>> BuildRootfixDispatchFromBreadthFirstAsync<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildRootfixDispatchAsync(capture, rootFamilySurvey, survey).ConfigureAwait(false);
    }

    // The finisher: run the pass, then zip (values, arrivals) into the ScanResult
    // decoration.
    private static async ValueTask<AsyncPreorderArrayStore<ScanResult<TSource, TDispatch>>> BuildRootfixDispatchAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var (values, subtreeSizes, arrivals) = await RunRootfixDispatchPassAsync(source, rootFamilySurvey, survey).ConfigureAwait(false);

      var results = new ScanResult<TSource, TDispatch>[values.Length];
      for (var nodeIndex = 0; nodeIndex < results.Length; nodeIndex++)
        results[nodeIndex] = new ScanResult<TSource, TDispatch>(values[nodeIndex], arrivals[nodeIndex]);

      // The result store rides the SAME subtree-size array the capture produced -- the shape is
      // the source's, only the values changed.
      return new AsyncPreorderArrayStore<ScanResult<TSource, TDispatch>>(results, subtreeSizes);
    }

    // The shared dispatch pass, both operators' engine: capture, child-index, arrival
    // resolution, surveys, exactly-once validation. Returns the raw arrays; the callers differ
    // only in their finisher.
    private static async ValueTask<(TSource[] Values, int[] SubtreeSizes, TDispatch[] Arrivals)> RunRootfixDispatchPassAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      // Pass 1: the capture factory's raw form -- one depth-first walk into the flat pre-order
      // encoding (a node's children sit at subtree-size hops after it). No positions array:
      // coordinates are DERIVED (sibling index = span offset, depth = the walk's ancestor
      // count) -- the perf re-baseline priced the stored array at 8 bytes/node and the
      // derivation at nothing.
      var (values, subtreeSizes) = await AsyncPreorderCapture
        .CaptureRawAsync(source)
        .ConfigureAwait(false);

      // Pass 2: top-down over the flat encoding. Preorder puts every parent before its children,
      // so each family's arrival is resolved before its own survey runs. The one written-flags
      // array carries the exactly-once bookkeeping for the whole build: every node -- roots
      // included, as the virtual root family's children -- is some family's child exactly once,
      // so no slot is ever reused and nothing per-node is allocated.
      var nodeCount = values.Length;
      var arrivals = new TDispatch[nodeCount];
      var written = new bool[nodeCount];

      // Pass 1½: the builds' shared child-index (DispatchChildIndex -- CSR over the preorder
      // encoding) buys the survey view its honestly-O(1) Count and indexer.
      var (childOffsets, childIndices) = DispatchChildIndex.Build(subtreeSizes);

      // FULL PARTICIPATION (2026-08-04): the virtual forest root's family goes first, through
      // the same survey as every other family (the seed flavor) or the selector sugar -- the
      // roots gathered into a one-family index so the boundary speaks the same
      // sibling-complete view and obeys the same exactly-once protocol.
      var rootCount = 0;
      for (var rootIndex = 0; rootIndex < nodeCount; rootIndex += subtreeSizes[rootIndex])
        rootCount++;

      var rootIndices = new int[rootCount];
      var nextRootSlot = 0;
      for (var rootIndex = 0; rootIndex < nodeCount; rootIndex += subtreeSizes[rootIndex])
        rootIndices[nextRootSlot++] = rootIndex;

      rootFamilySurvey(new DispatchTargets<TSource, TDispatch>(values, rootIndices, new[] { 0, rootCount }, arrivals, written, 0, childDepth: 0));

      for (var slot = 0; slot < rootCount; slot++)
        if (!written[rootIndices[slot]])
          throw new InvalidOperationException(
            $"The survey completed without dispatching to root '{values[rootIndices[slot]]}'; every root must receive exactly one Dispatch (the virtual forest root's family).");

      // The ancestor stack carries each surveyed family's depth: entries are the open
      // subtrees' end indices, so a node's depth is the count of spans still covering it.
      // Leaves are never pushed (nothing ever sits inside a leaf's span).
      var openSubtreeEnds = new Stack<int>();

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        while (openSubtreeEnds.Count > 0 && openSubtreeEnds.Peek() <= nodeIndex)
          openSubtreeEnds.Pop();

        if (subtreeSizes[nodeIndex] == 1)
          continue;

        var depth = openSubtreeEnds.Count;
        openSubtreeEnds.Push(nodeIndex + subtreeSizes[nodeIndex]);

        survey(
          arrivals[nodeIndex],
          new DispatchTargets<TSource, TDispatch>(values, childIndices, childOffsets, arrivals, written, nodeIndex, childDepth: depth + 1));

        // The survey returned; every child must have been dispatched to exactly once.
        for (var slot = childOffsets[nodeIndex]; slot < childOffsets[nodeIndex + 1]; slot++)
          if (!written[childIndices[slot]])
            throw new InvalidOperationException(
              $"The survey completed without dispatching to '{values[childIndices[slot]]}'; every child must receive exactly one Dispatch.");
      }

      return (values, subtreeSizes, arrivals);
    }
  }
}
