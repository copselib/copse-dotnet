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
    /// fold-shaped tier is RootfixScan): arrivals resolve top-down (roots receive
    /// <paramref name="seed"/>; every other node receives what its parent dispatched to it), and
    /// each internal node's <paramref name="survey"/> sees its arrival together with ALL of its
    /// children at once through the no-copy <see cref="DispatchTargets{TSource, TDispatch}"/>
    /// view -- one write-handle per child, each of which must receive exactly one
    /// <see cref="DispatchTarget{TSource, TDispatch}.Dispatch"/> (a second throws immediately; a
    /// missed one throws when the survey returns). Sibling-complete visibility is the point: a
    /// fairness split cannot allocate its edges independently, and a setter-callback allocator
    /// plugs in verbatim -- <c>(child, amount) =&gt; child.Dispatch(amount)</c> IS its assignment
    /// callback. Leaves are not surveyed. Surveys run in depth-first preorder.
    ///
    /// <para>VALUE-flavored (2026-08-02): the survey receives the parent's VALUE, and the root
    /// selector is arity-split on the Select/Where grammar -- value flavor primary,
    /// (node, position) flavor for seeding by root ordinal. This is the surface the Do tier
    /// inherits, born on the house's value-flavor grammar. Child positions stay available
    /// per-target (<see cref="DispatchTarget{TSource, TDispatch}.Context"/>); a survey flavor
    /// that sees the PARENT's position waits for a real case.</para>
    ///
    /// <para>The result pairs every source value with what arrived at it
    /// (<see cref="DispatchNode{TSource, TDispatch}"/>) in the source tree's shape -- it
    /// DECORATES rather than replaces, so the flavors are compositions: project the pair away
    /// with Select for immutable values, or apply it with Do (then unwrap) for mutable ones.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> for LeaffixDispatch's
    /// reason, mirrored: the survey needs its FULL child list before the first child's value
    /// exists, and in a depth-first stream a parent's children are separated by entire sibling
    /// subtrees -- so the source is fully consumed before the first result visit can be
    /// published. Deferred: construction is pinned to the first treenumerator acquisition
    /// (Tree.Lazy), and the awaited build runs ONCE, on the first replay pull. The source is
    /// consumed depth-first only, so a streamed narrow source can dispatch.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch(source, _ => seed, survey);

    /// <summary>
    /// The forest-correct seeding form -- RootfixScan's rootNodeSelector overload, mirrored:
    /// EVERY root's arrival comes from <paramref name="rootNodeSelector"/> against that root's
    /// SOURCE context, so each tree of a forest seeds independently. The fixed-seed overload is
    /// this with a constant at the roots.
    /// </summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatch(source, (node, _) => rootNodeSelector(node), survey)), BufferLayout.Preorder);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatch(source, rootNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: the build's structure pass runs depth-first, which a
    /// level-order arrival cannot provide, so the source is captured (the same O(n) every
    /// RootfixDispatch pays, disclosed by the buffer return type) and the pass runs over the
    /// capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch(source, _ => seed, survey);

    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatchBreadthFirstSource(source, (node, _) => rootNodeSelector(node), survey)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => new AsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderRootfixDispatchBreadthFirstSource(source, rootNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey);

    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey);

    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
      => RootfixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey);

    // Preorder for BOTH dimensions, matching LeaffixDispatch's measured layout decision (see its
    // note: the breadth-first cross-decode tax over raw array stores is ~1.08x, not worth a
    // transpose).
    private static IAsyncTreenumerable<DispatchNode<TSource, TDispatch>> PreorderRootfixDispatch<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var dispatched = new AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>(
        () => BuildRootfixDispatchAsync(source, rootNodeSelector, survey));

      return new AsyncPreorderTreenumerable<DispatchNode<TSource, TDispatch>, AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>>(dispatched);
    }

    private static IAsyncTreenumerable<DispatchNode<TSource, TDispatch>> PreorderRootfixDispatchBreadthFirstSource<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var dispatched = new AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>(
        () => BuildRootfixDispatchFromBreadthFirstAsync(source, rootNodeSelector, survey));

      return new AsyncPreorderTreenumerable<DispatchNode<TSource, TDispatch>, AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>>(dispatched);
    }

    private static async ValueTask<AsyncPreorderArrayStore<DispatchNode<TSource, TDispatch>>> BuildRootfixDispatchFromBreadthFirstAsync<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildRootfixDispatchAsync(capture, rootNodeSelector, survey).ConfigureAwait(false);
    }

    // The pure finisher: run the shared pass, then zip (values, arrivals) into the DispatchNode
    // decoration. The Do finisher (AsyncTreenumerable.RootfixDoDispatch.cs) rides the same pass
    // and hands the same pairs to its store instead -- one build, two exits.
    private static async ValueTask<AsyncPreorderArrayStore<DispatchNode<TSource, TDispatch>>> BuildRootfixDispatchAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      var (values, subtreeSizes, arrivals) = await RunRootfixDispatchPassAsync(source, rootNodeSelector, survey).ConfigureAwait(false);

      var results = new DispatchNode<TSource, TDispatch>[values.Length];
      for (var nodeIndex = 0; nodeIndex < results.Length; nodeIndex++)
        results[nodeIndex] = new DispatchNode<TSource, TDispatch>(values[nodeIndex], arrivals[nodeIndex]);

      // The result store rides the SAME subtree-size array the capture produced -- the shape is
      // the source's, only the values changed.
      return new AsyncPreorderArrayStore<DispatchNode<TSource, TDispatch>>(results, subtreeSizes);
    }

    // The shared dispatch pass, both operators' engine: capture, child-index, arrival
    // resolution, surveys, exactly-once validation. Returns the raw arrays; the callers differ
    // only in their finisher.
    private static async ValueTask<(TSource[] Values, int[] SubtreeSizes, TDispatch[] Arrivals)> RunRootfixDispatchPassAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey)
    {
      // Pass 1: the capture factory's raw form -- one depth-first walk into the flat pre-order
      // encoding (a node's children sit at subtree-size hops after it), positions riding the
      // side channel so nothing is stored twice.
      var (values, subtreeSizes, positions) = await AsyncPreorderCapture
        .CaptureRawAsync(source, nodeContext => nodeContext.Position)
        .ConfigureAwait(false);

      // Pass 2: top-down over the flat encoding. Preorder puts every parent before its children,
      // so each node's arrival is resolved before its own survey runs; roots (index 0 and every
      // whole-subtree hop after a root) are seeded first. The one written-flags array carries
      // the exactly-once bookkeeping for the whole build: every non-root is some parent's child
      // exactly once, so no slot is ever reused and nothing per-node is allocated.
      var nodeCount = values.Length;
      var arrivals = new TDispatch[nodeCount];
      var written = new bool[nodeCount];

      // Pass 1½: gather the child-index -- each parent's children's preorder indices as one
      // contiguous slice (CSR over the preorder encoding). Two O(n) hop passes and ~2n ints
      // buy the survey view an honestly-O(1) Count and indexer (2026-08-02; previously the
      // view span-hopped with no indexer, and indexing scenarios paid a ToArray per survey).
      var childOffsets = new int[nodeCount + 1];
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
          childOffsets[parentIndex + 1]++;
      }

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        childOffsets[nodeIndex + 1] += childOffsets[nodeIndex];

      // Parents are filled in ascending order, which IS offset order, so one cursor suffices.
      var childIndices = new int[childOffsets[nodeCount]];
      var childCursor = 0;
      for (var parentIndex = 0; parentIndex < nodeCount; parentIndex++)
      {
        var hopEnd = parentIndex + subtreeSizes[parentIndex];
        for (var childIndex = parentIndex + 1; childIndex < hopEnd; childIndex += subtreeSizes[childIndex])
          childIndices[childCursor++] = childIndex;
      }

      for (var rootIndex = 0; rootIndex < nodeCount; rootIndex += subtreeSizes[rootIndex])
        arrivals[rootIndex] = rootNodeSelector(values[rootIndex], positions[rootIndex]);

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        if (subtreeSizes[nodeIndex] == 1)
          continue;

        survey(
          values[nodeIndex],
          arrivals[nodeIndex],
          new DispatchTargets<TSource, TDispatch>(values, positions, childIndices, childOffsets, arrivals, written, nodeIndex));

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
