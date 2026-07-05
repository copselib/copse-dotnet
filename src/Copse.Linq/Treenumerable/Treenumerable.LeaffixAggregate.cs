using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Bottom-up aggregate: like LeaffixScan, but yields each root's accumulated value instead of a
    // tree. Lazy per root -- a root is emitted the moment its subtree completes, and the flat
    // buffers are then reused for the next root, so peak memory is the largest root subtree (not
    // the whole forest) and a consumer that stops early traverses fewer roots. Zero per-node alloc:
    // children are read via the no-copy ChildAccumulations view (see LeaffixScan).
    public static IEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator)
    {
      var accumulations = new List<TAccumulate>();
      var subtreeSizes = new List<int>();
      var path = new Stack<PendingNode<TSource>>();

      void Close()
      {
        var pending = path.Pop();
        var index = pending.Index;
        subtreeSizes[index] = accumulations.Count - index;
        accumulations[index] =
          subtreeSizes[index] == 1
          ? leafSelector(pending.Context)
          : accumulator(pending.Context, new ChildAccumulations<TAccumulate>(accumulations, subtreeSizes, index));
      }

      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          while (path.Count > depth)
            Close();

          // Back at the root level with a finished previous root buffered: emit it, reuse buffers.
          if (depth == 0 && accumulations.Count > 0)
          {
            yield return accumulations[0];
            accumulations.Clear();
            subtreeSizes.Clear();
          }

          path.Push(new PendingNode<TSource>(accumulations.Count, treenumerator.ToNodeContext()));
          accumulations.Add(default);
          subtreeSizes.Add(0);
        }
      }

      while (path.Count > 0)
        Close();

      if (accumulations.Count > 0)
        yield return accumulations[0];
    }
  }
}
