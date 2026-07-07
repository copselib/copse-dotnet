using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The root-to-leaf accumulations (RootfixScan, then the leaves), as a lazy async sequence -- one
    /// value per leaf, the fold of the accumulator down that root-to-leaf path.
    /// </summary>
    public static IAsyncEnumerable<TAccumulate> RootfixAggregate<TNode, TAccumulate>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
      => source.RootfixScan(accumulator, seed).GetLeaves();
  }
}
