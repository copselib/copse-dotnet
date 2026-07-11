using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's value becomes the
    /// accumulator applied to its parent's accumulated value and the node (a prefix-fold down each
    /// root-to-node path). Transforms the <c>TNode</c> tree into a <c>TAccumulate</c> tree. Deferred.
    /// The <paramref name="seed"/> is the virtual forest root's accumulation, so it is SHARED by
    /// every root of a forest; for per-root seeding use the rootNodeSelector overload.
    /// </summary>
    public static IAsyncTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.Create(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          accumulator,
          seed),
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IAsyncDepthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IAsyncBreadthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => AsyncTreenumerableFactory.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          accumulator,
          seed));

    /// <summary>
    /// The forest-correct seeding form: the boundary condition takes the seed's slot as a
    /// per-root function -- EVERY root starts its accumulation from
    /// <paramref name="rootNodeSelector"/>, so each tree of a forest seeds independently, and the
    /// accumulator only ever sees real parents (never a fabricated forest-root context). The
    /// single-seed overload is this with a constant at the roots; LeaffixScan's leafNodeSelector
    /// is the same fringe-answers-for-itself collapse at the other end of the tree.
    /// </summary>
    public static IAsyncTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      Func<NodeContext<TNode>, TAccumulate> rootNodeSelector)
      // The engines still park a sentinel seed, but under this form it is NEVER READ: the wrapped
      // accumulator routes every root to the selector off the sentinel's POSITION alone, and
      // nothing else reads the sentinel's value -- default is the "no seed exists here" placeholder.
      => source.RootfixScan(AccumulatorWithRootSelector(accumulator, rootNodeSelector), default(TAccumulate));

    public static IAsyncDepthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      Func<NodeContext<TNode>, TAccumulate> rootNodeSelector)
      => source.RootfixScan(AccumulatorWithRootSelector(accumulator, rootNodeSelector), default(TAccumulate));

    public static IAsyncBreadthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      Func<NodeContext<TNode>, TAccumulate> rootNodeSelector)
      => source.RootfixScan(AccumulatorWithRootSelector(accumulator, rootNodeSelector), default(TAccumulate));

    // The root dispatch, written once here so consumers never hand-roll the forest-root check
    // inside their accumulators: a root (parent context at the virtual forest root, where the
    // engines park the seed) takes the selector; every real parent flows through the accumulator
    // unchanged. The unused seed is default -- the selector branch is the only reader of roots.
    private static Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> AccumulatorWithRootSelector<TNode, TAccumulate>(
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      Func<NodeContext<TNode>, TAccumulate> rootNodeSelector)
      => (parentAccumulation, nodeContext) =>
        parentAccumulation.Position.IsForestRoot
        ? rootNodeSelector(nodeContext)
        : accumulator(parentAccumulation, nodeContext);
  }
}
