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
    /// forest (a rootNodeSelector form can join once the signature settles). Spike posture:
    /// rides the pure scan's machinery, whose treenumerators invoke the accumulator once per
    /// node at scheduling -- the store contract holds by construction; a graduated build gets
    /// its own treenumerators (or a pinned clause on the scan's).</para>
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
