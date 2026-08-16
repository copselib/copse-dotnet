using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The root-to-leaf accumulations (RootfixScan, then the leaves), as a lazy async sequence --
    /// one <see cref="NodeAccumulation{TSource, TAccumulate}"/> per leaf: the leaf's value paired with
    /// the fold of the accumulator down its root-to-leaf path (the canonical pairing,
    /// design-docs/SCANRESULT_DESIGN.md -- project <c>.Accumulate</c> when only values are wanted).
    /// </summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(seed, accumulator)
        .GetLeaves();
    }

    /// <summary>The breadth-first dual: leaf pairings in level order.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(seed, accumulator)
        .GetLeaves();
    }

    /// <summary>Disambiguation overload for full trees; keeps the historical depth-first behavior.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      TAccumulate seed,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixAggregate((IAsyncDepthFirstTreenumerable<TNode>)source, seed, accumulator);

    /// <summary>
    /// The forest-correct seeding form (see the RootfixScan rootNodeSelector overloads): every
    /// root seeds its own accumulation, so each tree of a forest folds independently.
    /// </summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(rootNodeSelector, accumulator)
        .GetLeaves();
    }

    /// <summary>The breadth-first dual: per-root-seeded leaf pairings in level order.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(rootNodeSelector, accumulator)
        .GetLeaves();
    }

    /// <summary>Disambiguation overload for full trees; keeps the historical depth-first behavior.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixAggregate((IAsyncDepthFirstTreenumerable<TNode>)source, rootNodeSelector, accumulator);

    /// <summary>The positional selector flavor: seeding by root ordinal.</summary>
    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(rootNodeSelector, accumulator)
        .GetLeaves();
    }

    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
    {
      return
        source
        .RootfixScan(rootNodeSelector, accumulator)
        .GetLeaves();
    }

    public static IAsyncEnumerable<NodeAccumulation<TNode, TAccumulate>> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<TNode, NodePosition, TAccumulate> rootNodeSelector,
      Func<TAccumulate, TNode, TAccumulate> accumulator)
      => RootfixAggregate((IAsyncDepthFirstTreenumerable<TNode>)source, rootNodeSelector, accumulator);
  }
}
