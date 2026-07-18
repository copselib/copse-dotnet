using Copse.Core;
using Copse.Core.Async;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // The level-order dual of AsyncPreorderCapture: one awaited breadth-first walk of any source,
  // captured into a completed level-order store. The parse state is the memo buffer's single
  // monotonic front cursor (see AsyncMemoizeLevelOrderStore for the full derivation): BFT
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
    /// <see cref="AsyncLevelOrderArrayStore{TValue}"/>. Eager: the walk runs now; wrap the call in a
    /// deferral seam (<c>AsyncLazyLevelOrderStore</c> behind <c>Tree.Lazy</c>) to pin it
    /// to first use. Finite sources only, like every capture.
    /// </summary>
    public static async ValueTask<AsyncLevelOrderArrayStore<TValue>> CaptureFromAsync<TValue>(
      IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      var values = new RefAppendOnlyList<TValue>();
      var firstChildIndices = new RefAppendOnlyList<int>();
      var childCounts = new RefAppendOnlyList<int>();
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

            values.AddLast(treenumerator.Node);
            firstChildIndices.AddLast(-1); // set when this node's first child arrives
            childCounts.AddLast(0);

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

      return new AsyncLevelOrderArrayStore<TValue>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }

    /// <summary>
    /// The stream-shaped overload: drains an <see cref="IAsyncLevelOrderStream{TValue}"/> --
    /// which already speaks the store's positional contract (group 0 the roots, group j+1 node
    /// j's children, items in level order) -- straight into a completed store. No visit stream
    /// is ever synthesized between the encodings (the FlatDecode family prices that round trip;
    /// this is the one-shot form of the drain the stream-fed store used to do incrementally).
    /// Takes ownership: the stream (and whatever it owns) is disposed on return.
    /// </summary>
    public static async ValueTask<AsyncLevelOrderArrayStore<TValue>> CaptureFromAsync<TValue>(
      IAsyncLevelOrderStream<TValue> stream)
    {
      var values = new RefAppendOnlyList<TValue>();
      var firstChildIndices = new RefAppendOnlyList<int>();
      var childCounts = new RefAppendOnlyList<int>();
      var rootCount = 0;
      var currentGroup = 0;

      await using (stream.ConfigureAwait(false))
      {
        while (true)
        {
          var read = await stream.TryReadNextInGroupAsync().ConfigureAwait(false);

          if (read.HasValue)
          {
            var index = values.Count;

            values.AddLast(read.Value);
            firstChildIndices.AddLast(-1); // set when this node's first child arrives
            childCounts.AddLast(0);

            if (currentGroup == 0)
            {
              rootCount++;
            }
            else
            {
              var owner = currentGroup - 1;

              if (childCounts[owner] == 0)
                firstChildIndices[owner] = index;

              childCounts[owner]++;
            }

            continue;
          }

          if (await stream.TryMoveToNextGroupAsync().ConfigureAwait(false))
          {
            currentGroup++;
            continue;
          }

          break;
        }
      }

      return new AsyncLevelOrderArrayStore<TValue>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }
  }
}
