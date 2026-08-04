using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (docs/SCANRESULT_DESIGN.md): a tree of <c>ScanResult</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (docs/SCANRESULT_DESIGN.md, ratified 2026-08-04): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTreenumerableFactory.Create(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncBreadthFirstTreenumerator,
          PairingAccumulator(accumulator),
          new ScanResult<TNode, TAccumulate>(default, seed)),
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncDepthFirstTreenumerator,
          PairingAccumulator(accumulator),
          new ScanResult<TNode, TAccumulate>(default, seed)));

    public static IAsyncDepthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncDepthFirstTreenumerator,
          PairingAccumulator(accumulator),
          new ScanResult<TNode, TAccumulate>(default, seed)));

    public static IAsyncBreadthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncBreadthFirstTreenumerator,
          PairingAccumulator(accumulator),
          new ScanResult<TNode, TAccumulate>(default, seed)));

    /// <summary>
    /// The forest-correct seeding form: EVERY root's accumulation comes from
    /// <paramref name="rootNodeSelector"/> against that root's value, so each tree of a forest
    /// seeds independently and the accumulator only ever sees real parents (never a fabricated
    /// forest-root pairing). The single-seed overload is this with a constant at the roots.
    /// </summary>
    public static IAsyncTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    public static IAsyncDepthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    public static IAsyncBreadthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      // The engines still park a sentinel seed, but under this form it is NEVER READ: the wrapped
      // accumulator routes every root to the selector off the sentinel's POSITION alone.
      => AsyncTreenumerableFactory.Create(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncBreadthFirstTreenumerator,
          PairingAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default),
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncDepthFirstTreenumerator,
          PairingAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default));

    public static IAsyncDepthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncDepthFirstTreenumerator,
          PairingAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default));

    public static IAsyncBreadthFirstTreenumerable<ScanResult<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, ScanResult<TNode, TAccumulate>>(
          source.GetAsyncBreadthFirstTreenumerator,
          PairingAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default));

    // The engine adapter: the generic scan treenumerators run with TAccumulate = the pairing
    // (the RESULT needs it), while the user accumulator speaks the minimal (accumulate, node)
    // basis -- the operator layer pairs on the way out, the engine stays untouched.
    private static Func<NodeContext<ScanResult<TNode, TAccumulate>>, NodeContext<TNode>, ScanResult<TNode, TAccumulate>> PairingAccumulator<TNode, TAccumulate>(
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => (parentPairing, nodeContext) =>
        new ScanResult<TNode, TAccumulate>(nodeContext.Node, accumulator(parentPairing.Node.Accumulate, nodeContext.Node));

    // The root dispatch, written once so consumers never hand-roll the forest-root check: a
    // root (parent pairing parked at the virtual forest root) takes the selector; every real
    // parent flows through the accumulator unchanged. The unused sentinel seed is default --
    // the selector branch is the only reader of roots.
    private static Func<NodeContext<ScanResult<TNode, TAccumulate>>, NodeContext<TNode>, ScanResult<TNode, TAccumulate>> PairingAccumulatorWithRootSelector<TNode, TAccumulate>(
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => (parentPairing, nodeContext) =>
        parentPairing.Position.IsForestRoot
        ? new ScanResult<TNode, TAccumulate>(nodeContext.Node, rootNodeSelector(nodeContext.Node, nodeContext.Position))
        : new ScanResult<TNode, TAccumulate>(nodeContext.Node, accumulator(parentPairing.Node.Accumulate, nodeContext.Node));
  }
}
