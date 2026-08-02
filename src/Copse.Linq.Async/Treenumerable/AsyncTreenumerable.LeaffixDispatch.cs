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
    /// The sibling-complete tier of the leaffix pair: every node gets an accumulated value --
    /// leaves from <paramref name="leafNodeSelector"/>, internal nodes from
    /// <paramref name="survey"/>, which sees ALL of its children's accumulated values at once
    /// through the no-copy <see cref="ChildAccumulations{TAccumulate}"/> view (subtree-span
    /// hops, zero per-node allocation). Sibling-complete visibility is the point -- median of
    /// children, top-k, anything that must compare children to each other, and boundary-only
    /// contributions like leaf count; a fold that only needs one child at a time belongs to
    /// LeaffixScan (sugar over this operator). The result tree has the same shape as the
    /// source. This is the upward dual of RootfixScan: the survey runs once per node receiving
    /// everything that arrives (n children up, versus one parent value down), and the boundary
    /// where nothing arrives -- leaves -- takes <paramref name="leafNodeSelector"/> (or the
    /// fixed <c>seed</c> overload), mirroring rootfix's rootNodeSelector/seed pair.
    ///
    /// <para>Callbacks run during the deferred build, once per node; the view enumerates
    /// children in sibling order. Invocation timing relative to the source walk is not
    /// specified, so callbacks should be pure.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because LeaffixDispatch
    /// MANUFACTURES owned O(n) storage: the surveyed accumulations are new values that exist
    /// nowhere in the source, and a root's value IS its whole subtree's aggregate -- the source
    /// is fully consumed before the first result visit can be published, so the result is a
    /// completed capture, not a lazy stream. Deferred (hence the sync name): construction is
    /// pinned to the first treenumerator acquisition (Tree.Lazy), and the awaited build runs
    /// ONCE, on the first replay pull, through the lazy-built store's grow seam. The source is
    /// consumed depth-first only, so a streamed narrow source can leaffix.</para>
    ///
    /// <para>Single forward DFS pass into flat pre-order arrays; see the sync operator for the
    /// construction notes (subtree-size hop, O(depth) working set).</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => new AsyncTreenumerableBuffer<TAccumulate>(
        AsyncTree.Lazy(() => PreorderDispatch(source, leafNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: a leaffix survey runs in depth-first subtree-close
    /// order, which a level-order arrival cannot provide, so the source is captured (the same
    /// O(n) every LeaffixDispatch pays, disclosed by the buffer return type) and the survey runs
    /// over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => new AsyncTreenumerableBuffer<TAccumulate>(
        AsyncTree.Lazy(() => PreorderDispatchBreadthFirstSource(source, leafNodeSelector, survey)), BufferLayout.Preorder);

    /// <summary>Disambiguation overload for full trees; keeps the historical depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, survey);

    /// <summary>
    /// The fixed-seed form -- RootfixScan's constant-seed overload, mirrored: every leaf starts
    /// the accumulation at <paramref name="seed"/>. The canonical use is boundary-only
    /// contribution, e.g. leaf count (seed 1, survey sums children).
    /// </summary>
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, _ => seed, survey);

    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch(source, _ => seed, survey);

    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      TAccumulate seed,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
      => LeaffixDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, _ => seed, survey);

    // Preorder for BOTH dimensions, deliberately: pinning a level-order layout on a
    // breadth-first-first pull (Tree.Lazy's dimension dispatch, one transpose pass into
    // LevelOrderArrayStore) was built and MEASURED OUT -- over raw array stores the
    // breadth-first cross-decode tax is only ~1.08x (the Memoize replay rows' 1.53x is
    // memo-store overhead, not layout), so the transpose plus transient double storage
    // needs ~5 replays to break even and taxes the common single-drain case ~8%.
    private static IAsyncTreenumerable<TAccumulate> PreorderDispatch<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
    {
      var surveyed = new AsyncLazyPreorderStore<TAccumulate>(
        () => BuildLeaffixDispatchAsync(source, leafNodeSelector, survey));

      return new AsyncPreorderTreenumerable<TAccumulate, AsyncLazyPreorderStore<TAccumulate>>(surveyed);
    }

    private static IAsyncTreenumerable<TAccumulate> PreorderDispatchBreadthFirstSource<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
    {
      var surveyed = new AsyncLazyPreorderStore<TAccumulate>(
        () => BuildLeaffixDispatchFromBreadthFirstAsync(source, leafNodeSelector, survey));

      return new AsyncPreorderTreenumerable<TAccumulate, AsyncLazyPreorderStore<TAccumulate>>(surveyed);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TAccumulate>> BuildLeaffixDispatchFromBreadthFirstAsync<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildLeaffixDispatchAsync(capture, leafNodeSelector, survey).ConfigureAwait(false);
    }

    // The pure finisher over the shared fold pass. The Do finisher
    // (AsyncTreenumerable.LeaffixDoDispatch.cs) rides the same pass with a pass-through values
    // sink and hands the (value, accumulation) pairs to its store instead -- one build, two
    // exits, the rootfix pair's arrangement mirrored.
    private static async ValueTask<AsyncPreorderArrayStore<TAccumulate>> BuildLeaffixDispatchAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey)
    {
      var (accumulations, subtreeSizes) =
        await RunLeaffixDispatchPassAsync(source, leafNodeSelector, survey, passThroughValues: null).ConfigureAwait(false);

      return new AsyncPreorderArrayStore<TAccumulate>(accumulations, subtreeSizes);
    }

    // The shared fold pass, both operators' engine: one depth-first walk folding into flat
    // pre-order slots, each node closing (leaf via selector, internal via survey over the
    // ChildAccumulations view) when the walk returns to its depth. The optional values sink
    // collects the source values in slot order for the Do twin's pass-through result; the pure
    // caller passes null and pays nothing for the sharing.
    private static async ValueTask<(TAccumulate[] Accumulations, int[] SubtreeSizes)> RunLeaffixDispatchPassAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> survey,
      List<TSource> passThroughValues)
    {
      var accumulations = new List<TAccumulate>();
      var subtreeSizes = new List<int>();
      var path = new Stack<PendingNode<TSource>>(); // open ancestors of the current node

      void Close()
      {
        var pending = path.Pop();
        var index = pending.Index;

        subtreeSizes[index] = accumulations.Count - index;
        accumulations[index] =
          subtreeSizes[index] == 1
          ? leafNodeSelector(pending.Context)
          : survey(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
      }

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          // Returning to this depth (or shallower) means every deeper open node is complete.
          while (path.Count > treenumerator.Position.Depth)
            Close();

          path.Push(new PendingNode<TSource>(accumulations.Count, treenumerator.ToNodeContext()));
          accumulations.Add(default); // backfilled when this node closes
          subtreeSizes.Add(0);
          passThroughValues?.Add(treenumerator.Node);
        }
      }

      while (path.Count > 0)
        Close();

      return (accumulations.ToArray(), subtreeSizes.ToArray());
    }
  }
}
