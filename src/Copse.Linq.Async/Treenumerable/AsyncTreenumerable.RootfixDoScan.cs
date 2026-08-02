using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The IMPURE rootfix scan (SPIKE, feature/do-scan): a downward cumulative pass whose point
    /// is its SIDE EFFECTS -- the Do idiom, scan-shaped. Nodes pass through unchanged (Do means
    /// the nodes ARE the result; no <see cref="ScanResult{TSource, TAccumulate}"/> travels --
    /// a packaged accumulate would duplicate what <paramref name="store"/> landed), and
    /// <typeparamref name="TAccumulate"/> is internal plumbing: <paramref name="seed"/> fixes
    /// it, <paramref name="compute"/> threads it down each root-to-node path, and
    /// <paramref name="store"/> -- the declared effect point -- lands each node's accumulation
    /// wherever the caller wants it, typically a property on a mutable node.
    ///
    /// <para>Two lambdas, two contracts -- the purity boundary sits between them.
    /// <paramref name="compute"/> is PURE with the scan family's permissive clause (invocation
    /// counts unspecified). <paramref name="store"/> fires EXACTLY ONCE per node per traversal,
    /// in that traversal's scheduling order, receiving the (node, accumulation) pairing -- the
    /// tightened clause that makes caching meaningful. The split also makes input/output
    /// separation the natural idiom (compute reads pristine inputs, store writes outputs), so a
    /// pass whose read and write fields are distinct is safely re-runnable; read-modify-write
    /// is something a caller must write deliberately, never something this shape hands out.</para>
    ///
    /// <para>Deferred and IMPURE BY DECLARATION: effects fire on EVERY traversal -- each drain
    /// of each dimension is a traversal -- which is sometimes exactly what the caller wants
    /// (recompute after mutating the tree between drains). A caller who wants the effects
    /// pinned to one run says so with the existing escalation vocabulary:
    /// <c>Memoize</c>/<c>Materialize</c> drain the source once and replay thereafter. Like
    /// <c>Do</c>, this operator is a composition barrier: store observes the visit stream at
    /// this point in the chain, so nothing may fuse across it.</para>
    ///
    /// <para>The seed is the virtual forest root's accumulation, shared by every root of a
    /// forest; the rootNodeSelector overloads (value and positional flavors, the family
    /// grammar) seed each root independently -- and on the Do tier the selector is also the
    /// FRESHNESS form (the seed-semantics-follow-purity rule): it runs during each traversal,
    /// so a closure reads live state at effect time, where a seed VALUE is frozen at the call.
    /// Under the selector form roots take the selector directly and <paramref name="compute"/>
    /// never sees a fabricated arrival (the pure scan's forest-correct clause, inherited).</para>
    ///
    /// <para>RootfixDoDispatch is this operator's sibling-complete twin. The implementations
    /// deliberately DIVERGE (ruled 2026-08-02): the fold tier streams (this operator rides the
    /// pure scan, whose ScanResult pairing carries the (node, accumulation) pair natively --
    /// the ScanResult sweep retired the internal tuple), the survey tier captures. Spike
    /// posture: the scan's treenumerators invoke the accumulator once per node at scheduling
    /// -- the store contract holds by construction.</para>
    /// </summary>
    public static IAsyncTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source.RootfixScan(seed, ComputeStoreAccumulator(compute, store)).Select(pairing => pairing.Node);

    public static IAsyncDepthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source.RootfixScan(seed, ComputeStoreAccumulator(compute, store)).Select(pairing => pairing.Node);

    public static IAsyncBreadthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source.RootfixScan(seed, ComputeStoreAccumulator(compute, store)).Select(pairing => pairing.Node);

    /// <summary>
    /// The forest-correct seeding form: every root's accumulation comes from
    /// <paramref name="rootNodeSelector"/> (and is stored) -- each tree of a forest seeds
    /// independently, and on this tier the selector doubles as the freshness form: it fires
    /// per root per traversal, so a closure reads live state at effect time.
    /// </summary>
    public static IAsyncTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => RootfixDoScan(source, (node, _) => rootNodeSelector(node), compute, store);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(SelectorWithStore(rootNodeSelector, store), ComputeStoreAccumulator(compute, store))
        .Select(pairing => pairing.Node);

    public static IAsyncDepthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => RootfixDoScan(source, (node, _) => rootNodeSelector(node), compute, store);

    public static IAsyncDepthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(SelectorWithStore(rootNodeSelector, store), ComputeStoreAccumulator(compute, store))
        .Select(pairing => pairing.Node);

    public static IAsyncBreadthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => RootfixDoScan(source, (node, _) => rootNodeSelector(node), compute, store);

    public static IAsyncBreadthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(SelectorWithStore(rootNodeSelector, store), ComputeStoreAccumulator(compute, store))
        .Select(pairing => pairing.Node);

    // The pass expressed over the pure scan, whose ScanResult pairing IS the (node, accumulate)
    // pair store needs -- the ScanResult sweep retired the internal tuple. The sentinel pairing
    // at the roots carries the seed as its Accumulate; store runs only for real nodes.
    private static Func<ScanResult<TNode, TAccumulate>, TNode, TAccumulate> ComputeStoreAccumulator<TNode, TAccumulate>(
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => (parentPairing, node) =>
      {
        var accumulate = compute(parentPairing.Accumulate, node);
        store(node, accumulate);
        return accumulate;
      };

    // The selector's wrapper: seed the root's accumulation and store it -- the root-side half
    // of the pass, invoked by the pure scan's forest-correct machinery once per root per
    // traversal (compute never sees a fabricated arrival under this form).
    private static Func<TNode, NodePosition, TAccumulate> SelectorWithStore<TNode, TAccumulate>(
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Action<TNode, TAccumulate> store)
      => (rootNode, rootPosition) =>
      {
        var accumulate = rootNodeSelector(rootNode, rootPosition);
        store(rootNode, accumulate);
        return accumulate;
      };
  }
}
