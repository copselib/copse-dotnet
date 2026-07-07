using Copse.Core;
using Copse.Linq.Extensions;
using Copse.Treenumerables;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Bottom-up scan: every node gets an accumulated value -- leaves from leafNodeSelector,
    // internal nodes from accumulator over their children's accumulated values. The result tree
    // has the same shape as the source.
    //
    // The O(n) build is the operator's own contract, not a hidden escalation: a root's value IS
    // its whole subtree's aggregate, and the result's scheduling visits emit values pre-order,
    // so the source must be fully consumed before the first visit can be published (leaffix
    // values flow against every arrival order). The build runs ONCE, lazily, on the first
    // treenumerator acquisition; the result is a full ITreenumerable honestly (completed
    // preorder arrays are random access). The source is consumed depth-first only, so a
    // streamed narrow source can leaffix.
    //
    // Single forward DFS pass into flat pre-order arrays. A node is accumulated the moment its
    // subtree closes (when the next scheduled node is at the same or a shallower depth), at which
    // point its descendants already hold their results at higher indices -- so children are read
    // straight from the accumulation array via the subtree-size hop (ChildAccumulations), with no
    // per-node temporary array. Working set during the build is just the current path (O(depth));
    // allocations are the two result arrays plus that path stack -- nothing per node.
    public static ITreenumerable<TAccumulate> LeaffixScan<TSource, TAccumulate>(
      this IDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
    {
      var scanned = new Lazy<PreorderTreenumerable<TAccumulate, PreorderArrayStore<TAccumulate>>>(
        () => BuildLeaffixScan(source, leafNodeSelector, accumulator));

      return TreenumerableFactory.Create(
        () => scanned.Value.GetBreadthFirstTreenumerator(),
        () => scanned.Value.GetDepthFirstTreenumerator());
    }

    private static PreorderTreenumerable<TAccumulate, PreorderArrayStore<TAccumulate>> BuildLeaffixScan<TSource, TAccumulate>(
      IDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafNodeSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
    {
      var accumulations = new List<TAccumulate>();
      var subtreeSizes = new List<int>();
      var path = new Stack<PendingNode<TSource>>(); // open ancestors of the current node

      void Close()
      {
        var pending = path.Pop();
        var index = pending.Index;
        subtreeSizes[index] = accumulations.Count - index;
        accumulations[index] =
          subtreeSizes[index] == 1
          ? leafNodeSelector(pending.Context)
          : accumulator(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
      }

      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          // Returning to this depth (or shallower) means every deeper open node is complete.
          while (path.Count > treenumerator.Position.Depth)
            Close();

          path.Push(new PendingNode<TSource>(accumulations.Count, treenumerator.ToNodeContext()));
          accumulations.Add(default); // backfilled when this node closes
          subtreeSizes.Add(0);
        }
      }

      while (path.Count > 0)
        Close();

      return new PreorderTreenumerable<TAccumulate, PreorderArrayStore<TAccumulate>>(
        new PreorderArrayStore<TAccumulate>(accumulations.ToArray(), subtreeSizes.ToArray()));
    }
  }
}
