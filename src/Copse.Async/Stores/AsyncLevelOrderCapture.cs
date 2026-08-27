using Copse.Collections;
using Copse.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Stores
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
  /// <summary>Captures any breadth-first source into a completed level-order array store --
  /// the flat family's encode direction, level-order form.</summary>
  public static class AsyncLevelOrderCapture
  {
    /// <summary>
    /// Captures the source -- one awaited breadth-first walk, TraverseAll -- into a completed
    /// <see cref="AsyncLevelOrderArrayStore{TNode}"/>. Eager: the walk runs now; wrap the call in a
    /// deferral seam (<c>AsyncLazyLevelOrderStore</c> behind <c>Tree.Lazy</c>) to pin it
    /// to first use. Finite sources only, like every capture.
    /// </summary>
    public static async ValueTask<AsyncLevelOrderArrayStore<TNode>> CaptureFromAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var values = new RefAppendOnlyList<TNode>();
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

      return new AsyncLevelOrderArrayStore<TNode>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }

    /// <summary>
    /// The counted fast path: as <c>CaptureFromAsync(source)</c>,
    /// with the node count known in advance -- the three final arrays are allocated exactly and
    /// the chunked build buffers are skipped, so the capture's transient allocation drops from
    /// ~2n to 1n. The count is a CONTRACT, not a hint: callers read it off a completed
    /// same-tree store, and a mismatch is a caller bug kept loud -- an undercount overruns the
    /// arrays, an overcount fails the closing check.
    /// </summary>
    public static async ValueTask<AsyncLevelOrderArrayStore<TNode>> CaptureFromAsync<TNode>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      int nodeCount)
    {
      var values = new TNode[nodeCount];
      var firstChildIndices = new int[nodeCount];
      var childCounts = new int[nodeCount];
      var rootCount = 0;
      var frontIndex = -1;
      var nextIndex = 0;

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            values[nextIndex] = treenumerator.Node;
            firstChildIndices[nextIndex] = -1; // set when this node's first child arrives

            if (treenumerator.Position.Depth == 0)
            {
              rootCount++;
            }
            else
            {
              if (childCounts[frontIndex] == 0)
                firstChildIndices[frontIndex] = nextIndex;

              childCounts[frontIndex]++;
            }

            nextIndex++;
          }
          else if (treenumerator.VisitCount == 1)
          {
            frontIndex++;
          }
        }
      }

      if (nextIndex != nodeCount)
        throw new InvalidOperationException(
          $"Counted capture walked {nextIndex} nodes; the caller promised {nodeCount}.");

      return new AsyncLevelOrderArrayStore<TNode>(values, firstChildIndices, childCounts, rootCount);
    }

    /// <summary>
    /// The stream-shaped overload: drains an <see cref="IAsyncLevelOrderStream{TNode}"/> --
    /// which already speaks the store's positional contract (group 0 the roots, group j+1 node
    /// j's children, items in level order) -- straight into a completed store. No visit stream
    /// is ever synthesized between the encodings (the FlatDecode family prices that round trip;
    /// the one-shot drain).
    /// Takes ownership: the stream (and whatever it owns) is disposed on return.
    /// </summary>
    public static async ValueTask<AsyncLevelOrderArrayStore<TNode>> CaptureFromAsync<TNode>(
      IAsyncLevelOrderStream<TNode> stream)
    {
      var values = new RefAppendOnlyList<TNode>();
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

      return new AsyncLevelOrderArrayStore<TNode>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }
  }
}
