using Copse.Async.Stores;
using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Stores;
using System;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The IMPURE sibling-complete upward pass (SPIKE, feature/do-scan): LeaffixDispatch's Do
    /// twin, for the mutable-node workload -- subtree rollups landing directly on the caller's
    /// objects. Nodes pass through unchanged (the result is the SOURCE tree; no accumulation
    /// tree ever reaches the caller), and each node's accumulation lands via
    /// <paramref name="store"/>.
    ///
    /// <para>Two callbacks, two contracts -- the purity boundary sits between them.
    /// <paramref name="survey"/> is PURE with the pure operator's identical shape, value-flavored:
    /// the node's value and ALL of its children's accumulations at once through the no-copy
    /// <see cref="DispatchSources{TSource, TAccumulate}"/> view; leaves take <paramref name="seed"/>
    /// (or the leafNodeSelector flavors) instead. <paramref name="store"/> is the declared
    /// effect point: it fires EXACTLY ONCE per node per build, in preorder order after the fold
    /// completes, receiving the (node, accumulation) pairing -- leaves and internal nodes
    /// alike, so coverage is total.</para>
    ///
    /// <para>Effect count follows the operator's laziness class, which the return type
    /// discloses: BOTH leaffix tiers are captures (children-first -- the whole tree precedes
    /// the first result), so the effects fire ONCE per operator instance, at the first drain --
    /// replays never re-fire; <c>Tree.Defer</c> is the re-run vocabulary. (This is why
    /// LeaffixDoScan and RootfixDoScan differ: effect count is a property of the laziness
    /// class, not of the Do marker.) Like <c>Do</c>, a composition barrier.</para>
    ///
    /// <para>Rides the pure operator's shared fold pass and is STRICTLY CHEAPER than it:
    /// pass-through needs no result storage of its own -- the returned buffer reuses the
    /// capture's value and subtree-size arrays as-is.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, _ => seed, survey, store);

    /// <summary>
    /// The per-leaf seeding form: every leaf's accumulation comes from
    /// <paramref name="leafNodeSelector"/> -- the fringe answers for itself. On the Do tier the
    /// selector is also the freshness form: it runs during the build, so a closure reads live
    /// state at effect time.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, (node, _) => leafNodeSelector(node), survey, store);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the leaf's value and its position.</summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderLeaffixDoDispatch(source, leafNodeSelector, survey, store)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the disclosure rule's escalation, mirrored
    /// from the pure operator: the fold runs in depth-first subtree-close order, so the source
    /// is captured and the pass runs over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, _ => seed, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch(source, (node, _) => leafNodeSelector(node), survey, store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderLeaffixDoDispatchBreadthFirstSource(source, leafNodeSelector, survey, store)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      TAccumulate seed,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> LeaffixDoDispatch<TSource, TAccumulate>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
      => LeaffixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, leafNodeSelector, survey, store);

    private static IAsyncTreenumerable<TSource> PreorderLeaffixDoDispatch<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildLeaffixDoDispatchAsync(source, leafNodeSelector, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static IAsyncTreenumerable<TSource> PreorderLeaffixDoDispatchBreadthFirstSource<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildLeaffixDoDispatchFromBreadthFirstAsync(source, leafNodeSelector, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildLeaffixDoDispatchFromBreadthFirstAsync<TSource, TAccumulate>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildLeaffixDoDispatchAsync(capture, leafNodeSelector, survey, store).ConfigureAwait(false);
    }

    // The Do finisher over the shared fold pass: where the pure build zips the pairs into its
    // ScanResult decoration, this one hands each (value, accumulation) pair to store --
    // preorder order after the fold completes, exactly once per node -- and the result reuses
    // the capture's own arrays: pass-through needs no storage of its own.
    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildLeaffixDoDispatchAsync<TSource, TAccumulate>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TAccumulate> leafNodeSelector,
      Func<TSource, DispatchSources<TSource, TAccumulate>, TAccumulate> survey,
      Action<TSource, TAccumulate> store)
    {
      var (values, subtreeSizes, accumulations) =
        await RunLeaffixDispatchPassAsync(source, leafNodeSelector, survey).ConfigureAwait(false);

      for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        store(values[nodeIndex], accumulations[nodeIndex]);

      return new AsyncPreorderArrayStore<TSource>(values, subtreeSizes);
    }
  }
}
