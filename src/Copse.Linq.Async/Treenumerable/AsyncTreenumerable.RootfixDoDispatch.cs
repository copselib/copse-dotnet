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
    /// The IMPURE survey-shaped downward pass: RootfixDispatch's Do twin, for the mutable-node
    /// workload. Nodes pass through unchanged (the result is the SOURCE tree; no
    /// <see cref="ScanResult{TSource, TDispatch}"/> decoration ever reaches the caller -- Do
    /// means the nodes ARE the result), and the flow lands where the caller wants it via
    /// <paramref name="store"/>.
    ///
    /// <para>ONE dispatcher for every family (full participation, unified 2026-08-04):
    /// <paramref name="survey"/> receives <c>(arrival, members)</c> -- the virtual forest
    /// root's family first (<paramref name="seed"/> as its arrival, the roots as its
    /// sibling-complete targets), then every internal family in preorder. The surveyed
    /// family's parent VALUE holds no seat: it is derivable -- flow any subject-shaped fact
    /// inside <typeparamref name="TDispatch"/> at the dispatch site, where the node is in hand
    /// as the target's <c>.Node</c> (the seat rule; see RootfixDispatch's doc).</para>
    ///
    /// <para>THE DELIVERY MODEL (ratified 2026-08-04; re-founded same day): <c>Dispatch</c>
    /// DELIVERS, and every delivery lands on your entity via <paramref name="store"/> -- the
    /// pure operator's <c>Dispatch</c> writes into the result pairing; this one writes onto
    /// YOUR object, via the landing rule you declare once. Every node receives exactly one
    /// delivery from its family's survey -- roots included, from the virtual root's family --
    /// so <paramref name="store"/> fires EXACTLY ONCE per node. SEQUENCING: stores fire in
    /// preorder, after the whole pass completes and validates (missed and doubled slots throw
    /// during the surveys). Corollaries the caller can derive, disclosed rather than promised:
    /// a failed PASS lands nothing; a throwing STORE leaves the preorder prefix already
    /// landed. The survey stays pure and shares the pure operator's exact shape -- a
    /// setter-callback allocator plugs in verbatim (<c>(child, amount) =&gt;
    /// child.Dispatch(amount)</c> IS its assignment callback).</para>
    ///
    /// <para>WHY <c>Dispatch</c> TAKES A VALUE, NOT THE MUTATION (the operator's most natural
    /// misreading -- asked twice by the library's own author, so it will be asked by every
    /// consumer): dispatching <c>child =&gt; mutate(child)</c> instead of a value fails three
    /// ways. (1) Landing and dispatching become two acts -- <c>dt.Node.X = v; dt.Dispatch(v)</c>
    /// -- and the second is forgettable per call site where <paramref name="store"/> is
    /// declared once. (2) The value channel dies -- each survey would have to read its
    /// family's arrival off an entity field, so any quantity you did not want persisted on
    /// every entity would need a scratch field on YOUR domain type;
    /// <typeparamref name="TDispatch"/> is the library-provided scratch channel, and it lets
    /// flow and field diverge. (3) A closure per child per node, where the value form writes
    /// into a slot. WHY <paramref name="store"/> EXISTS AT ALL: the survey's writes are
    /// edge-grained deliveries into machinery slots; <paramref name="store"/> is the
    /// node-grained landing rule, declared once, applied to every node after validation.</para>
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
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatch(source, targets => survey(seed, targets), survey, store)), BufferLayout.Preorder);

    /// <summary>
    /// The per-root seeding flavor -- boundary sugar for roots that follow a DIFFERENT,
    /// per-root rule than the survey: every root's arrival comes from
    /// <paramref name="rootNodeSelector"/> in isolation. On the Do tier the selector is also
    /// the FRESHNESS form (the seed-semantics-follow-purity rule): it runs during the build,
    /// so a closure reads live state at effect time.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatch(source, PerRootSurvey<TSource, TDispatch>((node, _) => rootNodeSelector(node)), survey, store)), BufferLayout.Preorder);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatch(source, PerRootSurvey<TSource, TDispatch>(rootNodeSelector), survey, store)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the disclosure rule's escalation, mirrored
    /// from the pure operator: the pass runs depth-first, so the source is captured and the
    /// pass runs over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatchBreadthFirstSource(source, targets => survey(seed, targets), survey, store)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatchBreadthFirstSource(source, PerRootSurvey<TSource, TDispatch>((node, _) => rootNodeSelector(node)), survey, store)), BufferLayout.Preorder);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncBreadthFirstTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => new AsyncTreenumerableBuffer<TSource>(
        AsyncTree.Lazy(() => PreorderRootfixDoDispatchBreadthFirstSource(source, PerRootSurvey<TSource, TDispatch>(rootNodeSelector), survey, store)), BufferLayout.Preorder);

    /// <summary>Disambiguation overloads for full trees; keep the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      TDispatch seed,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, seed, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey, store);

    public static IAsyncTreenumerableBuffer<TSource> RootfixDoDispatch<TSource, TDispatch>(
      this IAsyncTreenumerable<TSource> source,
      Func<TSource, NodePosition, TDispatch> rootNodeSelector,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
      => RootfixDoDispatch((IAsyncDepthFirstTreenumerable<TSource>)source, rootNodeSelector, survey, store);

    private static IAsyncTreenumerable<TSource> PreorderRootfixDoDispatch<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildRootfixDoDispatchAsync(source, rootFamilySurvey, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static IAsyncTreenumerable<TSource> PreorderRootfixDoDispatchBreadthFirstSource<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var stored = new AsyncLazyPreorderStore<TSource>(
        () => BuildRootfixDoDispatchFromBreadthFirstAsync(source, rootFamilySurvey, survey, store));

      return new AsyncPreorderTreenumerable<TSource, AsyncLazyPreorderStore<TSource>>(stored);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildRootfixDoDispatchFromBreadthFirstAsync<TSource, TDispatch>(
      IAsyncBreadthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildRootfixDoDispatchAsync(capture, rootFamilySurvey, survey, store).ConfigureAwait(false);
    }

    // The Do finisher over the shared dispatch pass: where the pure build zips each
    // (value, arrival) pair into its decoration, this one hands the same pair to store --
    // preorder order, exactly once per node -- and the result reuses the capture's own arrays:
    // pass-through needs no storage of its own.
    private static async ValueTask<AsyncPreorderArrayStore<TSource>> BuildRootfixDoDispatchAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      Action<DispatchTargets<TSource, TDispatch>> rootFamilySurvey,
      Action<TDispatch, DispatchTargets<TSource, TDispatch>> survey,
      Action<TSource, TDispatch> store)
    {
      var (values, subtreeSizes, arrivals) = await RunRootfixDispatchPassAsync(source, rootFamilySurvey, survey).ConfigureAwait(false);

      for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        store(values[nodeIndex], arrivals[nodeIndex]);

      return new AsyncPreorderArrayStore<TSource>(values, subtreeSizes);
    }
  }
}
