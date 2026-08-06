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
  // docs/OPERATOR_SURFACE_MAP.md section 3 -- hoisted to its one home.
  //
  // Nodes are appended on their SCHEDULING visit; the open-node stack backfills each subtree
  // size when depth retreats (subtreeSizes[i] == 0 marks a still-open node until then).
  // Appending on the first VISITING visit instead is equivalent in a depth-first walk (a node's
  // first visit immediately follows its scheduling); the memo buffers use that form. Scheduling
  // is the operator convention, standardized here.
  public static class AsyncPreorderCapture
  {
    /// <summary>
    /// Captures the source -- one awaited depth-first walk, TraverseAll -- into a completed
    /// <see cref="AsyncPreorderArrayStore{TValue}"/>. Eager: the walk runs now; wrap the call in a
    /// deferral seam (<c>AsyncLazyPreorderStore</c> behind <c>Tree.Lazy</c>) to pin it to
    /// first use, the way the capture operators do. Finite sources only, like every capture.
    /// </summary>
    public static async ValueTask<AsyncPreorderArrayStore<TValue>> CaptureFromAsync<TValue>(
      IAsyncDepthFirstTreenumerable<TValue> source)
    {
      var (values, subtreeSizes) = await CaptureCoreAsync<TValue, bool>(source, sideChannelSelector: null, sideChannel: null).ConfigureAwait(false);

      return new AsyncPreorderArrayStore<TValue>(values, subtreeSizes);
    }

    /// <summary>
    /// As <c>CaptureFromAsync(source)</c>, additionally evaluating
    /// <paramref name="sideChannelSelector"/> exactly once per node -- during the capture,
    /// against the SOURCE context -- into a preorder-parallel array (element i belongs to store
    /// node i). The hook for capture operators that need a per-node companion value
    /// (OrderChildrenBy's sort keys).
    /// </summary>
    public static async ValueTask<(AsyncPreorderArrayStore<TValue> Store, TSide[] SideChannel)> CaptureFromAsync<TValue, TSide>(
      IAsyncDepthFirstTreenumerable<TValue> source,
      Func<NodeContext<TValue>, TSide> sideChannelSelector)
    {
      var sideChannel = new RefAppendOnlyList<TSide>();
      var (values, subtreeSizes) = await CaptureCoreAsync(source, sideChannelSelector, sideChannel).ConfigureAwait(false);

      return (new AsyncPreorderArrayStore<TValue>(values, subtreeSizes), sideChannel.ToArray());
    }

    /// <summary>
    /// The NAKED encoding: as the side-channel form, but returning the walk's raw
    /// preorder-parallel arrays instead of wrapping them in a store -- for consumers that weave
    /// a DIFFERENT store out of the walk (RootfixDispatch surveys over the encoding, then
    /// builds a NodeArrival store from the same subtree-size array). <c>Values[i]</c> in
    /// preorder; node i's subtree spans <c>[i, i + SubtreeSizes[i])</c>;
    /// <c>SideChannel[i]</c> evaluated once per node against the source context.
    /// </summary>
    public static async ValueTask<(TValue[] Values, int[] SubtreeSizes, TSide[] SideChannel)> CaptureRawAsync<TValue, TSide>(
      IAsyncDepthFirstTreenumerable<TValue> source,
      Func<NodeContext<TValue>, TSide> sideChannelSelector)
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
    public static async ValueTask<(TValue[] Values, int[] SubtreeSizes)> CaptureRawAsync<TValue>(
      IAsyncDepthFirstTreenumerable<TValue> source)
      => await CaptureCoreAsync<TValue, int>(source, null, null).ConfigureAwait(false);

    private static async ValueTask<(TValue[] Values, int[] SubtreeSizes)> CaptureCoreAsync<TValue, TSide>(
      IAsyncDepthFirstTreenumerable<TValue> source,
      Func<NodeContext<TValue>, TSide> sideChannelSelector,
      RefAppendOnlyList<TSide> sideChannel)
    {
      var values = new RefAppendOnlyList<TValue>();
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
          sideChannel?.AddLast(sideChannelSelector(new NodeContext<TValue>(treenumerator.Node, treenumerator.Position)));
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
