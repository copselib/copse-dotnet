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
    /// The IMPURE survey-shaped downward pass (SPIKE, feature/do-scan): RootfixDispatch's Do
    /// twin, for the mutable-node workload. Nodes pass through unchanged (the result is the
    /// SOURCE tree; no <see cref="ScanResult{TSource, TDispatch}"/> decoration ever reaches
    /// the caller -- Do means the nodes ARE the result), and the flow lands where the caller wants it via <paramref name="store"/>.
    ///
    /// <para>THE DELIVERY MODEL (ratified 2026-08-04; re-founded same day): <c>Dispatch</c>
    /// DELIVERS, and every delivery lands on your entity via <paramref name="store"/> -- the
    /// pure operator's <c>Dispatch</c> writes into the result pairing; this one writes onto
    /// YOUR object, via the landing rule you declare once. The <paramref name="seed"/> is a
    /// delivery to the roots (so it lands like every other delivery -- never land it by hand
    /// in the selector). Every node receives exactly one delivery -- roots the seed, every
    /// other node its parent's dispatch -- so <paramref name="store"/> fires EXACTLY ONCE per
    /// node. SEQUENCING: stores fire in preorder, after the whole pass completes and
    /// validates (missed and doubled slots throw during the surveys). Corollaries the caller
    /// can derive, disclosed rather than promised: a failed PASS lands nothing; a throwing
    /// STORE leaves the preorder prefix already landed. The survey stays pure and shares the
    /// pure operator's exact shape -- a setter-callback allocator plugs in verbatim
    /// (<c>(child, amount) =&gt; child.Dispatch(amount)</c> IS its assignment callback).</para>
    ///
    /// <para>WHY <c>Dispatch</c> TAKES A VALUE, NOT THE MUTATION (the operator's most natural
    /// misreading -- asked twice by the library's own author, so it will be asked by every
    /// consumer): dispatching <c>child =&gt; mutate(child)</c> instead of a value fails three
    /// ways. (1) The seed has no deliverer -- with no value channel the seed cannot enter as
    /// data, so roots become a special case needing their own landing syntax; the uniform
    /// <paramref name="store"/> is the dissolution of that special case. (2) The value channel
    /// dies for everyone -- each survey would have to read its subject's field to know what to
    /// subdivide, so any quantity you did not want persisted on every entity would need a
    /// scratch field on YOUR domain type; <typeparamref name="TDispatch"/> is the
    /// library-provided scratch channel, and it lets flow and field diverge. (3) A closure per
    /// child per node, where the value form writes into a slot. WHY <paramref name="store"/>
    /// EXISTS AT ALL: the survey is a parent's sibling-complete operator -- leaves are never
    /// surveyed -- so <paramref name="store"/> is the only callback that reaches every node.
    /// (Its seat is structural; contrast RootfixDoScan, whose once-per-node fold lets landing
    /// ride the return and needs no store.)</para>
    ///
    /// <para>Effect count follows the operator's laziness class, which the return type
    /// discloses: a buffer is a deferred-once capture (Tree.Lazy pins the build to the first
    /// treenumerator acquisition), so the effects fire ONCE per operator instance, at first
    /// drain -- replays never re-fire. Re-running against mutated state is a fresh expression
    /// (or <c>Tree.Defer</c>, the disclosed freshness vocabulary). Like <c>Do</c>, this
    /// operator is a composition barrier: nothing may fuse across the declared effect point.</para>
    ///
    /// <para>Rides the pure operator's shared dispatch pass and is STRICTLY CHEAPER than it:
    /// pass-through needs no result storage of its own -- the returned buffer reuses the
    /// capture's value and subtree-size arrays as-is.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch(source, _ => seed, survey, store);

    /// <summary>
    /// The forest-correct seeding form: every root's arrival comes from
    /// <paramref name="rootNodeSelector"/>, so each tree of a forest seeds independently.
    /// On the Do tier the selector is also the FRESHNESS form (the seed-semantics-follow-purity
    /// rule): it runs during the build, so a closure reads live state at effect time.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatch(source, (node, _) => rootNodeSelector(node), survey, store)), BufferLayout.Preorder);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatch(source, rootNodeSelector, survey, store)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the disclosure rule's escalation, mirrored
    /// from the pure operator: the pass runs depth-first, so the source is captured and the
    /// pass runs over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch(source, _ => seed, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatchBreadthFirstSource(source, (node, _) => rootNodeSelector(node), survey, store)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatchBreadthFirstSource(source, rootNodeSelector, survey, store)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      TDispatch seed,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey, store);

    private static IAsyncTreenumerable<TSource> PreorderRootfixDoDispatch<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildRootfixDoDispatchAsync(source, rootNodeSelector, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static IAsyncTreenumerable<TSource> PreorderRootfixDoDispatchBreadthFirstSource<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildRootfixDoDispatchFromBreadthFirstAsync(source, rootNodeSelector, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildRootfixDoDispatchFromBreadthFirstAsync<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildRootfixDoDispatchAsync(capture, rootNodeSelector, survey, store).ConfigureAwait(false);
    }

    // The Do finisher over the shared dispatch pass: where the pure build zips each
    // (value, arrival) pair into its decoration, this one hands the same pair to store --
    // preorder order, exactly once per node -- and the result reuses the capture's own arrays:
    // pass-through needs no storage of its own.
    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildRootfixDoDispatchAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TSource, TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var (values, subtreeSizes, arrivals) = await RunRootfixDispatchPassAsync(source, rootNodeSelector, survey).ConfigureAwait(false);

      for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        store(values[nodeIndex], arrivals[nodeIndex]);

      return new AsyncPreorderArrayStore<TSource>(values, subtreeSizes);
    }
  }
}
