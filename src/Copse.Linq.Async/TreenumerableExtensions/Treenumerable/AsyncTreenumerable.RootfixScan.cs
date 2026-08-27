using Copse.Linq.Treenumerators;
using Copse;
using Copse.Treenumerables;
using Copse.Core;
using Copse.Linq;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      // The composite result is the streaming tier's citizen (the projection citizenship):
      // plain acquisitions construct exactly the engines this overload always constructed;
      // a composed Select re-plants the projection inside the product engine twins.
      => RootfixScanCitizen(source, ContextAccumulator(accumulator), seed);

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          ContextAccumulator(accumulator),
          seed));

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTree.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          ContextAccumulator(accumulator),
          seed));

    /// <summary>
    /// The per-root flavor -- A DIFFERENT INSTRUMENT than the seed flavor, not a different
    /// spelling of it (THE NORTH STAR: boundary flavors mean the same thing on
    /// both tiers -- design-docs/SCANRESULT_DESIGN.md): every root's ACCUMULATION is
    /// <paramref name="rootNodeSelector"/>'s return, set DIRECTLY -- the fold fires only at
    /// non-roots -- exactly as RootfixDispatch's selector sets each root's arrival directly,
    /// bypassing the survey. Set each tree's starting value explicitly (known per-root
    /// budgets); the SEED flavor is the other instrument -- the virtual root's arrival,
    /// transformed by the fold at every node (<c>accumulator(seed, root)</c>), one value the
    /// tier's callback speaks over. Consequently <c>RootfixScan(seed, fold)</c> is NOT
    /// <c>RootfixScan(_ =&gt; seed, fold)</c> -- pinned deliberately-different, mirroring the
    /// dispatch tier's pin -- and <c>RootfixScan(boundary, fold)</c> IS
    /// <c>RootfixDispatch(boundary, (a, dts) =&gt; { foreach (var dt in dts)
    /// dt.Dispatch(fold(a, dt.Node)); })</c> for EVERY boundary flavor
    /// (CrossTierCoherenceTests, the invariant's battery).
    /// </summary>
    public static IAsyncTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is
    /// <paramref name="rootNodeSelector"/> applied to the root -- per-root seeding; the seed
    /// overloads share one accumulate across a forest's roots.</para>
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is
    /// <paramref name="rootNodeSelector"/> applied to the root -- per-root seeding; the seed
    /// overloads share one accumulate across a forest's roots.</para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixScan(source, (node, _) => rootNodeSelector(node), accumulator);

    /// <summary>The positional selector flavor (the Select/Where arity-split grammar): the root's value and its position -- seeding by root ordinal.</summary>
    public static IAsyncTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      // The engines still park a sentinel seed, but under this form it is NEVER READ: the wrapped
      // accumulator routes every root to the selector off the sentinel's POSITION alone.
      // Citizen-shaped like the seed flavor (the rootNodeSelector flavor flows through here).
      => RootfixScanCitizen(source, ContextAccumulatorWithRootSelector(rootNodeSelector, accumulator), default);

    // COMPOSE-LEFT (the rootfix door -- "left of the scan", SCAN_TIER_DESIGN.md; the leaffix
    // door's streaming mirror): a pure-projection wrapper upstream surrenders its pieces and
    // the scan's citizen is built over the un-projected inner RAW -- the projection folds
    // into the accumulator (once per scheduled node) and rides the context-shaped product
    // selector at emission; ZERO wrapper layers on any pull, and the result is still the
    // citizen, so the whole left-composed chain keeps composing (Select into the engine,
    // rejecting operators into the fourth cell).
    private static IAsyncTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScanCitizen<TNode, TAccumulate>(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> contextAccumulator,
      TAccumulate seed)
    {
      if (source is IAsyncProjectionSource<TNode> projectionSource)
        return projectionSource.CaptureThrough(
          new AsyncRootfixFromProjectionConsumer<TNode, TAccumulate>(contextAccumulator, seed));

      return new AsyncRootfixScanTreenumerable<TNode, TAccumulate>(
        source.GetAsyncDepthFirstTreenumerator,
        source.GetAsyncBreadthFirstTreenumerator,
        contextAccumulator,
        seed);
    }

    // The consumer half of the rootfix door: builds the PRODUCT citizen over the surrendered
    // inner -- the projector runs inside the fold (per scheduled node) and inside the product
    // selector (per emission), exactly the counts the wrapper spelling paid, minus the
    // wrapper hop on every pull.
    private sealed class AsyncRootfixFromProjectionConsumer<TProjected, TAccumulate>
      : IAsyncProjectionConsumer<TProjected, IAsyncTreenumerable<NodeAccumulation<TProjected, TAccumulate>>>
    {
      public AsyncRootfixFromProjectionConsumer(
        Func<NodeContext<TAccumulate>, NodeContext<TProjected>, TAccumulate> contextAccumulator,
        TAccumulate seed)
      {
        _ContextAccumulator = contextAccumulator;
        _Seed = seed;
      }

      private readonly Func<NodeContext<TAccumulate>, NodeContext<TProjected>, TAccumulate> _ContextAccumulator;
      private readonly TAccumulate _Seed;

      public IAsyncTreenumerable<NodeAccumulation<TProjected, TAccumulate>> Consume<TInner>(
        IAsyncTreenumerable<TInner> innerSource,
        Func<NodeContext<TInner>, TProjected> projector)
      {
        var contextAccumulator = _ContextAccumulator;

        return new AsyncRootfixScanProductTreenumerable<TInner, TAccumulate, NodeAccumulation<TProjected, TAccumulate>>(
          innerSource.GetAsyncDepthFirstTreenumerator,
          innerSource.GetAsyncBreadthFirstTreenumerator,
          (parentContext, innerContext) => contextAccumulator(
            parentContext,
            new NodeContext<TProjected>(projector(innerContext), innerContext.Position)),
          _Seed,
          pairingContext => new NodeAccumulation<TProjected, TAccumulate>(
            projector(new NodeContext<TInner>(pairingContext.Node.Node, pairingContext.Position)),
            pairingContext.Node.Accumulate));
      }
    }

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncDepthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTree.CreateDepthFirst(
        () => new AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncDepthFirstTreenumerator,
          ContextAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default));

    /// <summary>
    /// Async <c>RootfixScan</c>: a cumulative scan from the root -- each node's accumulation is
    /// the accumulator applied to its parent's accumulation and the node's value (a prefix-fold
    /// down each root-to-node path). Returns the CANONICAL
    /// PAIRING (design-docs/SCANRESULT_DESIGN.md): a tree of <c>NodeAccumulation</c>s, each node's value
    /// with its accumulation -- project <c>.Accumulate</c> away when only values are wanted.
    /// Deferred; streams with O(depth)/O(width) state.
    ///
    /// <para>The accumulator is <c>(accumulate, node)</c> -- LINQ Aggregate's shape, and the
    /// SEAT RULE's minimal basis (design-docs/SCANRESULT_DESIGN.md): a callback
    /// receives its subject and its flow state, nothing derivable. <typeparamref name="TAccumulate"/>
    /// IS the caller's chosen summary of the root-to-node path -- a rule that wants the parent
    /// entity (or grandparent, or any ancestry) threads it through the state; a rule that is
    /// ABOUT the parent with its children in hand is a survey (RootfixDispatch). The pairing
    /// appears only in the RESULT. At the roots the accumulate is the <paramref name="seed"/>,
    /// SHARED by every root of a forest; for per-root seeding use the rootNodeSelector
    /// overloads.</para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<NodeAccumulation<TNode, TAccumulate>> RootfixScan<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => AsyncTree.CreateBreadthFirst(
        () => new AsyncRootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetAsyncBreadthFirstTreenumerator,
          ContextAccumulatorWithRootSelector(rootNodeSelector, accumulator),
          default));

    // The engine adapter: the engines run BARE -- their state is the fold's own width (THE
    // EMISSION MINT: the pairing is constructed per emission from the inner's
    // node-in-hand, never stored) -- while the user accumulator speaks the minimal
    // (accumulate, node) basis; this lifts it to the engine's context shape.
    private static Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> ContextAccumulator<TNode, TAccumulate>(
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => (parentContext, nodeContext) => accumulator(parentContext.Node, nodeContext.Node);

    // The root boundary, written once so consumers never hand-roll the forest-root check: a
    // root (parent context parked at the virtual forest root) takes the selector's return AS
    // its accumulation -- the bypass instrument, THE NORTH STAR's scan half:
    // cross-tier flavor coherence selects these semantics, because the dispatch selector sets
    // roots' arrivals directly and arrival IS the value there, so the fold-encoded dispatch
    // and this scan agree at roots only if the selector bypasses the fold. The alternative,
    // fold(selector(root), root), buys the lesser intra-tier equivalence at the cost of the
    // cross-tier one. The unused sentinel seed is default -- the selector branch is the only
    // reader of roots.
    private static Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> ContextAccumulatorWithRootSelector<TNode, TAccumulate>(
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => (parentContext, nodeContext) =>
        parentContext.Position.IsForestRoot
        ? rootNodeSelector(nodeContext.Node, nodeContext.Position)
        : accumulator(parentContext.Node, nodeContext.Node);
  }
}
