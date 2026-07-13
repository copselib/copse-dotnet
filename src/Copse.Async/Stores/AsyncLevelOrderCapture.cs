using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // The level-order dual of AsyncPreorderCapture: one awaited breadth-first walk of any source,
  // captured into a completed level-order store. The parse state is the memo buffer's single
  // monotonic front cursor (see AsyncMemoizeLevelOrderBuffer for the full derivation): BFT
  // visits nodes in the order they were scheduled, so the front -- advanced on each node's
  // first visiting visit -- is always the node whose children are currently being scheduled,
  // and every scheduled non-root wires into the front's child span. No stack, no search.
  //
  // No side-channel overload yet: the preorder side has a consumer (OrderChildrenBy's keys);
  // this side has none. Add the dual when one exists.
  public static class AsyncLevelOrderCapture
  {
    /// <summary>
    /// Captures the source -- one awaited breadth-first walk, TraverseAll -- into a completed
    /// <see cref="LevelOrderArrayStore{TValue}"/>. Eager: the walk runs now; wrap the call in a
    /// deferral seam (<c>AsyncLazyBuiltLevelOrderStore</c> behind <c>Tree.Lazy</c>) to pin it
    /// to first use. Finite sources only, like every capture.
    /// </summary>
    public static async ValueTask<LevelOrderArrayStore<TValue>> CaptureFromAsync<TValue>(
      IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      var values = new List<TValue>();
      var firstChildIndices = new List<int>();
      var childCounts = new List<int>();
      var rootCount = 0;
      var frontIndex = -1;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            var index = values.Count;

            values.Add(treenumerator.Node);
            firstChildIndices.Add(-1); // set when this node's first child arrives
            childCounts.Add(0);

            if (treenumerator.Position.Depth == 0)
            {
              rootCount++;
            }
            else
            {
              if (childCounts[frontIndex] == 0)
                firstChildIndices[frontIndex] = index;

              childCounts[frontIndex]++;
            }
          }
          else if (treenumerator.VisitCount == 1)
          {
            frontIndex++;
          }
        }
      }

      return new LevelOrderArrayStore<TValue>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }
  }
}
