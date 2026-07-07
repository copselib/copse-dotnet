using Copse;
using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The leaf-to-root accumulations (LeaffixScan collapsed to its roots), as a lazy async
    /// sequence -- one value per root tree, the fold of the accumulator up from that tree's leaves.
    /// Each node accumulates over its children's accumulations (or the leaf selector at a leaf).
    /// </summary>
    public static async IAsyncEnumerable<TAccumulate> LeaffixAggregate<TSource, TAccumulate>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
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

      var t = source.GetAsyncDepthFirstTreenumerator();
      await using (t.ConfigureAwait(false))
      {
        while (await t.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (t.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = t.Position.Depth;

          while (path.Count > depth)
            Close();

          if (depth == 0 && accumulations.Count > 0)
          {
            yield return accumulations[0];
            accumulations.Clear();
            subtreeSizes.Clear();
          }

          path.Push(new PendingNode<TSource>(accumulations.Count, new NodeContext<TSource>(t.Node, t.Position)));
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
