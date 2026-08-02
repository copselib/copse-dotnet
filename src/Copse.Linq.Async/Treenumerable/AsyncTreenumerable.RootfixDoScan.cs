using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The IMPURE rootfix scan (SPIKE, feature/do-scan): a downward cumulative pass whose point
    /// is its SIDE EFFECTS -- the Do idiom, scan-shaped. Nodes pass through unchanged (no
    /// projection; the result is the SOURCE tree), and <typeparamref name="TAccumulate"/> is
    /// internal plumbing: <paramref name="seed"/> fixes it, <paramref name="compute"/> threads
    /// it down each root-to-node path, and <paramref name="store"/> -- the declared effect
    /// point -- lands each node's accumulation wherever the caller wants it, typically a
    /// property on a mutable node.
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
    /// scan machinery, O(depth)/O(width) state, effects per drain), the survey tier captures
    /// (the dispatch build, effects once) -- the same cost-class asymmetry as the pure pair,
    /// so delegating this operator into the dispatch build would demote it from streaming to
    /// capture-class for nothing. Spike posture: rides the pure scan's machinery, whose
    /// treenumerators invoke the accumulator once per node at scheduling -- the store contract
    /// holds by construction; a graduated build gets its own treenumerators (or a pinned
    /// clause on the scan's).</para>
    /// </summary>
    public static IAsyncTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(
          (Node: default(TNode), Accumulate: seed),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

    public static IAsyncDepthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(
          (Node: default(TNode), Accumulate: seed),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

    public static IAsyncBreadthFirstTreenumerable<TNode> RootfixDoScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => source
        .RootfixScan(
          (Node: default(TNode), Accumulate: seed),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

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
        .RootfixScan(
          SelectorWithStore(rootNodeSelector, store),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

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
        .RootfixScan(
          SelectorWithStore(rootNodeSelector, store),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

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
        .RootfixScan(
          SelectorWithStore(rootNodeSelector, store),
          ComputeStoreAccumulator(compute, store))
        .Select(pair => pair.Node);

    // The selector's wrapper: seed the root's accumulation, store it, and thread the pairing --
    // the root-side half of the pass, invoked by the pure scan's forest-correct machinery once
    // per root per traversal (compute never sees a fabricated arrival under this form).
    private static Func<NodeContext<TNode>, (TNode Node, TAccumulate Accumulate)> SelectorWithStore<TNode, TAccumulate>(
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Action<TNode, TAccumulate> store)
      => rootContext =>
      {
        var accumulate = rootNodeSelector(rootContext.Node, rootContext.Position);
        store(rootContext.Node, accumulate);
        return (rootContext.Node, accumulate);
      };

    // The pass expressed over the pure scan: accumulate (node, accumulation) pairs so store
    // receives the pairing the pure composition cannot express without tuple plumbing, then
    // project the pass-through back out. The seed slot's default node is never observed --
    // store runs only inside the accumulator, which the scan invokes for real nodes only.
    private static Func<NodeContext<(TNode Node, TAccumulate Accumulate)>, NodeContext<TNode>, (TNode Node, TAccumulate Accumulate)> ComputeStoreAccumulator<TNode, TAccumulate>(
      Func<TAccumulate, TNode, TAccumulate> compute,
      Action<TNode, TAccumulate> store)
      => (parentPair, nodeContext) =>
      {
        var accumulate = compute(parentPair.Node.Accumulate, nodeContext.Node);
        store(nodeContext.Node, accumulate);
        return (nodeContext.Node, accumulate);
      };
  }
}
