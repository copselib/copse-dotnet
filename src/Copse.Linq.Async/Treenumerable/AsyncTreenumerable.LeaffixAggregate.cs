using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The leaf-to-root accumulations (LeaffixScan collapsed to its roots), as a lazy async
    /// sequence -- one value per root tree, the fold of the accumulator up from that tree's leaves.
    /// Each node accumulates over its children's accumulations (or the leaf selector at a leaf).
    /// Lazy per root -- a root is emitted the moment its subtree completes, and the flat buffers are
    /// then reused for the next root, so peak memory is the largest root subtree (not the whole
    /// forest) and a consumer that stops early traverses fewer roots. Zero per-node alloc: children
    /// are read via the no-copy ChildAccumulations view (see LeaffixScan).
    /// </summary>
    public static async IAsyncEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TAccumulate> leafSelector,
      Func<NodeContext<TSource>, ChildAccumulations<TAccumulate>, TAccumulate> accumulator,
      [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          while (path.Count > depth)
            Close();

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
