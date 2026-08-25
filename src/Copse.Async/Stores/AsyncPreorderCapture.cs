using Copse.Core;
using Copse.Core.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async.Stores
{
  // The flat family's ENCODE direction, written once: one awaited depth-first walk of any
  // source, captured into a completed preorder store. (The store treenumerators are the DECODE
  // direction; together they make the flat family self-contained in this layer.) This is the
  // capture loop that Invert, OrderChildrenBy, benchmarks, and tests each re-derived -- see
  // design-docs/OPERATOR_SURFACE_MAP.md section 3 -- hoisted to its one home.
  //
  // Nodes are appended on their SCHEDULING visit; the open-node stack backfills each subtree
  // size when depth retreats (subtreeSizes[i] == 0 marks a still-open node until then).
  // Appending on the first VISITING visit instead is equivalent in a depth-first walk (a node's
  // first visit immediately follows its scheduling); the memo buffers use that form. Scheduling
  // is the operator convention, standardized here.
  /// <summary>Captures any depth-first source into a completed preorder array store -- the
  /// flat family's encode direction.</summary>
  public static class AsyncPreorderCapture
  {
    /// <summary>
    /// Captures the source -- one awaited depth-first walk, TraverseAll -- into a completed
    /// <see cref="AsyncPreorderArrayStore{TNode}"/>. Eager: the walk runs now; wrap the call in a
    /// deferral seam (<c>AsyncLazyPreorderStore</c> behind <c>Tree.Lazy</c>) to pin it to
    /// first use, the way the capture operators do. Finite sources only, like every capture.
    /// </summary>
    public static async ValueTask<AsyncPreorderArrayStore<TNode>> CaptureFromAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var (values, subtreeSizes) = await CaptureCoreAsync<TNode, bool>(source, sideChannelSelector: null, sideChannel: null).ConfigureAwait(false);

      return new AsyncPreorderArrayStore<TNode>(values, subtreeSizes);
    }

    /// <summary>
    /// The counted fast path: as <c>CaptureFromAsync(source)</c>,
    /// with the node count known in advance -- the final arrays are allocated exactly and the
    /// chunked build buffer is skipped, so the capture's transient allocation drops from ~2n to
    /// 1n. The count is a CONTRACT, not a hint: callers read it off a completed same-tree store
    /// (a transpose source, a completed memo), and a mismatch is a caller bug kept loud -- an
    /// undercount overruns the array, an overcount fails the closing check.
    /// </summary>
    public static async ValueTask<AsyncPreorderArrayStore<TNode>> CaptureFromAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      int nodeCount)
    {
      var values = new TNode[nodeCount];
      var subtreeSizes = new int[nodeCount];
      var openNodes = new Stack<int>();
      var nextIndex = 0;

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          while (openNodes.Count > treenumerator.Position.Depth)
          {
            var closedNode = openNodes.Pop();
            subtreeSizes[closedNode] = nextIndex - closedNode;
          }

          openNodes.Push(nextIndex);
          values[nextIndex] = treenumerator.Node;
          nextIndex++;
        }
      }

      while (openNodes.Count > 0)
      {
        var closedNode = openNodes.Pop();
        subtreeSizes[closedNode] = nextIndex - closedNode;
      }

      if (nextIndex != nodeCount)
        throw new InvalidOperationException(
          $"Counted capture walked {nextIndex} nodes; the caller promised {nodeCount}.");

      return new AsyncPreorderArrayStore<TNode>(values, subtreeSizes);
    }

    /// <summary>
    /// As <c>CaptureFromAsync(source)</c>, additionally evaluating
    /// <paramref name="sideChannelSelector"/> exactly once per node -- during the capture,
    /// against the SOURCE context -- into a preorder-parallel array (element i belongs to store
    /// node i). The hook for capture operators that need a per-node companion value
    /// (OrderChildrenBy's sort keys).
    /// </summary>
    public static async ValueTask<(AsyncPreorderArrayStore<TNode> Store, TSide[] SideChannel)> CaptureFromAsync<TNode, TSide>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TSide> sideChannelSelector)
    {
      var sideChannel = new RefAppendOnlyList<TSide>();
      var (values, subtreeSizes) = await CaptureCoreAsync(source, sideChannelSelector, sideChannel).ConfigureAwait(false);

      return (new AsyncPreorderArrayStore<TNode>(values, subtreeSizes), sideChannel.ToArray());
    }

    /// <summary>
    /// The NAKED encoding: as the side-channel form, but returning the walk's raw
    /// preorder-parallel arrays instead of wrapping them in a store -- for consumers that weave
    /// a DIFFERENT store out of the walk (RootfixDispatch surveys over the encoding, then
    /// builds a NodeArrival store from the same subtree-size array). <c>Values[i]</c> in
    /// preorder; node i's subtree spans <c>[i, i + SubtreeSizes[i])</c>;
    /// <c>SideChannel[i]</c> evaluated once per node against the source context.
    /// </summary>
    public static async ValueTask<(TNode[] Values, int[] SubtreeSizes, TSide[] SideChannel)> CaptureRawAsync<TNode, TSide>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TSide> sideChannelSelector)
    {
      var sideChannel = new RefAppendOnlyList<TSide>();
      var (values, subtreeSizes) = await CaptureCoreAsync(source, sideChannelSelector, sideChannel).ConfigureAwait(false);

      return (values, subtreeSizes, sideChannel.ToArray());
    }

    /// <summary>
    /// The side-channel-free raw form: values and subtree sizes only -- for passes that derive
    /// coordinates from the encoding itself (a child's sibling index is its offset in the
    /// parent's span; depth threads through the walk) instead of storing them.
    /// </summary>
    public static async ValueTask<(TNode[] Values, int[] SubtreeSizes)> CaptureRawAsync<TNode>(
      IAsyncDepthFirstTreenumerable<TNode> source)
      => await CaptureCoreAsync<TNode, int>(source, null, null).ConfigureAwait(false);

    private static async ValueTask<(TNode[] Values, int[] SubtreeSizes)> CaptureCoreAsync<TNode, TSide>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TSide> sideChannelSelector,
      RefAppendOnlyList<TSide> sideChannel)
    {
      var values = new RefAppendOnlyList<TNode>();
      var subtreeSizes = new RefAppendOnlyList<int>();
      var openNodes = new Stack<int>();

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          while (openNodes.Count > treenumerator.Position.Depth)
          {
            var closedNode = openNodes.Pop();
            subtreeSizes[closedNode] = values.Count - closedNode;
          }

          openNodes.Push(values.Count);
          values.AddLast(treenumerator.Node);
          subtreeSizes.AddLast(0);
          sideChannel?.AddLast(sideChannelSelector(new NodeContext<TNode>(treenumerator.Node, treenumerator.Position)));
        }
      }

      while (openNodes.Count > 0)
      {
        var closedNode = openNodes.Pop();
        subtreeSizes[closedNode] = values.Count - closedNode;
      }

      return (values.ToArray(), subtreeSizes.ToArray());
    }
  }
}
