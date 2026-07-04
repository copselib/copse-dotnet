using Copse.Core;
using Copse.Linq.Treenumerators;
using System;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static ITreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => TreenumerableFactory.Create(
        () => new RootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetBreadthFirstTreenumerator,
          accumulator,
          seed),
        () => new RootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IDepthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => TreenumerableFactory.CreateDepthFirst(
        () => new RootfixScanDepthFirstTreenumerator<TNode, TAccumulate>(
          source.GetDepthFirstTreenumerator,
          accumulator,
          seed));

    public static IBreadthFirstTreenumerable<TAccumulate> RootfixScan<TNode, TAccumulate>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => TreenumerableFactory.CreateBreadthFirst(
        () => new RootfixScanBreadthFirstTreenumerator<TNode, TAccumulate>(
          source.GetBreadthFirstTreenumerator,
          accumulator,
          seed));
  }
}
