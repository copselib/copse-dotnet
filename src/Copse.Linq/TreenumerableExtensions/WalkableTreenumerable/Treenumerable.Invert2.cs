using Copse;
using Copse.Core;
using Copse.Linq.Stores;
using Copse.Linq.Treenumerables;
using Copse.Stores;
using Copse.Treenumerables;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // EXPERIMENT, second operator (2026-08-14, "let's see how far we can push this"):
    // Invert over the walker tier. The incumbent's buffer overload is already
    // receiver-aware at the TYPE level (a dedicated ITreenumerableBuffer overload) and its
    // build is already the span-arithmetic mirror -- but it re-captures the buffer from its
    // own visit stream first (AsyncPreorderCapture.CaptureFromAsync in BuildMirrorAsync),
    // because before the store seam there was no path to the arrays it stands on. This
    // spelling is that build minus the re-capture. Hand-written sync-only, not in the
    // manifest; lives or dies with its siblings.
    //
    // Receiver assumption (same as LeaffixScan2): handles are the capture's PREORDER
    // ordinals; the walker fallback's first pass computes subtree sizes by the same
    // descending-handle recurrence the leaffix fold uses.
    /// <summary>
    /// The walkable-receiver mirror: reverse every node's children (and the roots),
    /// identical semantics to <c>Invert</c>, computed from the receiver's own skeleton with
    /// no second capture. Deferred like the incumbent: the mirror is emitted at the
    /// result's first treenumerator acquisition.
    /// </summary>
    public static ITreenumerableBuffer<TNode> Invert2<TNode>(this ITreenumerableBuffer<TNode> source)
      => new TreenumerableBuffer<TNode>(
        new PreorderTreenumerable<TNode, LazyPreorderStore<TNode>>(
          new LazyPreorderStore<TNode>(() =>
            source is TreenumerableBuffer<TNode> concreteBuffer && concreteBuffer.TryGetPreorderStore(out var store)
              ? SpanMirror(store)
              : WalkerMirror(source))),
        BufferLayout.Preorder);

    // The incumbent's own emit, handed the store instead of re-capturing it: pushing
    // roots/children in forward order makes them pop in reverse, which is exactly the
    // mirror's preorder; each subtree keeps its size, only ordering changes.
    private static PreorderArrayStore<TNode> SpanMirror<TNode>(PreorderArrayStore<TNode> capture)
    {
      var count = capture.Count;
      var mirroredValues = new TNode[count];
      var mirroredSubtreeSizes = new int[count];
      var stack = new Stack<int>();

      for (var root = 0; root < count; root += capture.GetSubtreeSize(root))
        stack.Push(root);

      var output = 0;

      while (stack.Count > 0)
      {
        var index = stack.Pop();

        mirroredValues[output] = capture.GetValue(index);
        mirroredSubtreeSizes[output] = capture.GetSubtreeSize(index);
        output++;

        var end = index + capture.GetSubtreeSize(index);

        for (var child = index + 1; child < end; child += capture.GetSubtreeSize(child))
          stack.Push(child);
      }

      return new PreorderArrayStore<TNode>(mirroredValues, mirroredSubtreeSizes);
    }

    // The public-vocabulary fallback for a walkable capture that is not the concrete
    // buffer: pass one computes subtree sizes bottom-up (the leaffix recurrence -- children
    // complete before parents in descending preorder), pass two is the same LIFO emit with
    // stances and steps in place of span hops.
    private static PreorderArrayStore<TNode> WalkerMirror<TNode>(ITreenumerableBuffer<TNode> source)
    {
      var nodeCount = source.GetHandles().Count();

      var subtreeSizes = new int[nodeCount];

      for (var handle = nodeCount - 1; handle >= 0; handle--)
      {
        var stance = source.GetTreeWalkerAt(handle);

        var subtreeSize = 1;
        var step = stance.MoveToChild(0);

        for (var childIndex = 1; step.HasWalker; childIndex++)
        {
          subtreeSize += subtreeSizes[step.Walker.Focus];
          step = stance.MoveToChild(childIndex);
        }

        subtreeSizes[handle] = subtreeSize;
      }

      var mirroredValues = new TNode[nodeCount];
      var mirroredSubtreeSizes = new int[nodeCount];
      var stack = new Stack<int>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var root = source.TryGetRootAt(rootIndex);
        if (!root.HasChild)
          break;

        stack.Push(root.Child.Node);
      }

      var output = 0;

      while (stack.Count > 0)
      {
        var handle = stack.Pop();
        var stance = source.GetTreeWalkerAt(handle);

        mirroredValues[output] = stance.GetValue();
        mirroredSubtreeSizes[output] = subtreeSizes[handle];
        output++;

        var step = stance.MoveToChild(0);

        for (var childIndex = 1; step.HasWalker; childIndex++)
        {
          stack.Push(step.Walker.Focus);
          step = stance.MoveToChild(childIndex);
        }
      }

      return new PreorderArrayStore<TNode>(mirroredValues, mirroredSubtreeSizes);
    }
  }
}
