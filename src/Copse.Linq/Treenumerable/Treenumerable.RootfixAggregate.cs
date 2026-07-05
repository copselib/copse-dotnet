using Copse.Core;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    public static IEnumerable<TAccumulate> RootfixAggregate<TNode, TAccumulate>(
      this IDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
    {
      return
        source
        .RootfixScan(accumulator, seed)
        .GetLeaves();
    }

    // The breadth-first dual: leaf accumulations in level order.
    public static IEnumerable<TAccumulate> RootfixAggregate<TNode, TAccumulate>(
      this IBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
    {
      return
        source
        .RootfixScan(accumulator, seed)
        .GetLeaves();
    }

    // Disambiguation overload for full trees; keeps the historical depth-first behavior.
    public static IEnumerable<TAccumulate> RootfixAggregate<TNode, TAccumulate>(
      this ITreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => RootfixAggregate((IDepthFirstTreenumerable<TNode>)source, accumulator, seed);
  }
}
