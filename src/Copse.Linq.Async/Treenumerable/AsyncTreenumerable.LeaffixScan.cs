using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
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
    /// Bottom-up scan: every node gets an accumulated value -- leaves from
    /// <paramref name="leafNodeSelector"/>, internal nodes from <paramref name="accumulator"/>
    /// over their children's accumulated values. The result tree has the same shape as the source.
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> because LeaffixScan
    /// MANUFACTURES owned O(n) storage: the projected accumulations are new values that exist
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
    public static IAsyncTreenumerableBuffer<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
      => new AsyncCompletedTreenumerableBuffer<TAccumulate>(
        AsyncTree.Lazy(() => PreorderScan(source, leafNodeSelector, accumulator)));

    // Preorder for BOTH dimensions, deliberately: pinning a level-order layout on a
    // breadth-first-first pull (Tree.Lazy's dimension dispatch, one transpose pass into
    // LevelOrderArrayStore) was built and MEASURED OUT -- over raw array stores the
    // breadth-first cross-decode tax is only ~1.08x (the Memoize replay rows' 1.53x is
    // memo-store overhead, not layout), so the transpose plus transient double storage
    // needs ~5 replays to break even and taxes the common single-drain case ~8%.
    private static IAsyncTreenumerable<TAccumulate> PreorderScan<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
    {
      var scanned = new AsyncLazyBuiltPreorderStore<TAccumulate>(
        () => BuildLeaffixScanAsync(source, leafNodeSelector, accumulator));

      return new AsyncPreorderTreenumerable<TAccumulate, AsyncLazyBuiltPreorderStore<TAccumulate>>(scanned);
    }

    private static async ValueTask<PreorderArrayStore<TAccumulate>> BuildLeaffixScanAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
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
          : accumulator(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
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
        }
      }

      while (path.Count > 0)
        Close();

      return new PreorderArrayStore<TAccumulate>(accumulations.ToArray(), subtreeSizes.ToArray());
    }
  }
}
