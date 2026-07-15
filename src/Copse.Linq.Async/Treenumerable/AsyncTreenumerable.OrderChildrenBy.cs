using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerators;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Reorders every sibling group -- each node's children, and the roots -- ascending by key,
    /// STABLY (equal keys keep their original sibling order, so ordering refines the source order
    /// rather than scrambling it). Subtrees travel whole with their parents: only sibling order,
    /// and therefore sibling indexes, changes. The key selector runs exactly once per node,
    /// during capture, and receives the node's SOURCE context (its pre-ordering position).
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> for Invert's reason --
    /// ordering is the mirror generalized from "reverse every sibling group" to "sort every
    /// sibling group" (Invert IS this operator, descending by source sibling index): the first
    /// child in the new order may be the source's last, so whole sibling subtrees must be in hand
    /// before the first result visit can be published. One awaited depth-first walk captures flat
    /// preorder arrays; the ordered layout is then emitted by subtree-span hops. Deferred:
    /// construction is pinned to the first treenumerator acquisition (Tree.Lazy), and the awaited
    /// build runs once, on the first replay pull. The source is consumed depth-first only, so a
    /// streamed narrow source can order.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => source.OrderChildrenBy(keySelector, Comparer<TKey>.Default);

    /// <summary>As <c>OrderChildrenBy(keySelector)</c> with an explicit key comparer.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderOrderChildren(source, keySelector, comparer, descending: false)), BufferLayout.Preorder);

    /// <summary>
    /// The descending twin of <c>OrderChildrenBy(keySelector)</c>: every sibling group descending
    /// by key, still STABLE (equal keys keep their original sibling order -- not reversed).
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => source.OrderChildrenByDescending(keySelector, Comparer<TKey>.Default);

    /// <summary>As <c>OrderChildrenByDescending(keySelector)</c> with an explicit key comparer.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderOrderChildren(source, keySelector, comparer, descending: true)), BufferLayout.Preorder);

    /// <summary>
    /// The breadth-first-only source overload -- the DISCLOSURE RULE's escalation written once,
    /// here, instead of at every call site: the ordered emission needs random access to whole
    /// subtrees, which a level-order arrival cannot provide, so the source is captured (the same
    /// O(n) every OrderChildrenBy pays, disclosed by the buffer return type) and the build runs
    /// over the capture's depth-first replay.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => source.OrderChildrenBy(keySelector, Comparer<TKey>.Default);

    /// <summary>As the breadth-first <c>OrderChildrenBy(keySelector)</c> with an explicit key comparer.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderOrderChildrenBreadthFirstSource(source, keySelector, comparer, descending: false)), BufferLayout.Preorder);

    /// <summary>The descending twin of the breadth-first <c>OrderChildrenBy(keySelector)</c>.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => source.OrderChildrenByDescending(keySelector, Comparer<TKey>.Default);

    /// <summary>As the breadth-first <c>OrderChildrenByDescending(keySelector)</c> with an explicit key comparer.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderOrderChildrenBreadthFirstSource(source, keySelector, comparer, descending: true)), BufferLayout.Preorder);

    /// <summary>Disambiguation overload for full trees; keeps the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => OrderChildrenBy((IAsyncDepthFirstTreenumerable<TNode>)source, keySelector);

    /// <summary>Disambiguation overload for full trees; keeps the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenBy<TNode, TKey>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => OrderChildrenBy((IAsyncDepthFirstTreenumerable<TNode>)source, keySelector, comparer);

    /// <summary>Disambiguation overload for full trees; keeps the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector)
      => OrderChildrenByDescending((IAsyncDepthFirstTreenumerable<TNode>)source, keySelector);

    /// <summary>Disambiguation overload for full trees; keeps the depth-first consumption.</summary>
    public static IAsyncTreenumerableBuffer<TNode> OrderChildrenByDescending<TNode, TKey>(
      this IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer)
      => OrderChildrenByDescending((IAsyncDepthFirstTreenumerable<TNode>)source, keySelector, comparer);

    // Preorder for BOTH dimensions, matching LeaffixScan's measured layout decision.
    private static IAsyncTreenumerable<TNode> PreorderOrderChildren<TNode, TKey>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      var ordered = new AsyncLazyPreorderStore<TNode>(
        () => BuildOrderedChildrenAsync(source, keySelector, comparer, descending));

      return new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(ordered);
    }

    private static IAsyncTreenumerable<TNode> PreorderOrderChildrenBreadthFirstSource<TNode, TKey>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      var ordered = new AsyncLazyPreorderStore<TNode>(
        () => BuildOrderedChildrenFromBreadthFirstAsync(source, keySelector, comparer, descending));

      return new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(ordered);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildOrderedChildrenFromBreadthFirstAsync<TNode, TKey>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      var capture = await source.MaterializeAsync().ConfigureAwait(false);

      return await BuildOrderedChildrenAsync(capture, keySelector, comparer, descending).ConfigureAwait(false);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildOrderedChildrenAsync<TNode, TKey>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      // 1. Capture flat preorder arrays (value + subtree size per node -- Invert's capture) from
      //    one awaited depth-first walk of the source, evaluating the KEY once per node here.
      var values = new List<TNode>();
      var keys = new List<TKey>();
      var subtreeSizes = new List<int>();
      var open = new Stack<int>();

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          while (open.Count > treenumerator.Position.Depth)
          {
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
          }

          open.Push(values.Count);
          values.Add(treenumerator.Node);
          keys.Add(keySelector(treenumerator.ToNodeContext()));
          subtreeSizes.Add(0);
        }
      }

      while (open.Count > 0)
      {
        var closed = open.Pop();
        subtreeSizes[closed] = values.Count - closed;
      }

      // 2. Emit the ordered layout. Every sibling group (the roots, then each emitted node's
      //    children) is sorted by its cached keys -- OrderBy/OrderByDescending for guaranteed
      //    stability -- and pushed in REVERSE so the group pops in order. Each subtree keeps its
      //    size; only ordering changes.
      var count = values.Count;
      var orderedValues = new TNode[count];
      var orderedSubtreeSizes = new int[count];
      var stack = new Stack<int>();
      var siblingGroup = new List<int>();

      void PushSiblingGroupInOrder()
      {
        var orderedGroup =
          (descending
            ? siblingGroup.OrderByDescending(nodeIndex => keys[nodeIndex], comparer)
            : siblingGroup.OrderBy(nodeIndex => keys[nodeIndex], comparer))
          .ToList();

        for (var groupPosition = orderedGroup.Count - 1; groupPosition >= 0; groupPosition--)
          stack.Push(orderedGroup[groupPosition]);

        siblingGroup.Clear();
      }

      for (var root = 0; root < count; root += subtreeSizes[root])
        siblingGroup.Add(root);

      PushSiblingGroupInOrder();

      var output = 0;

      while (stack.Count > 0)
      {
        var index = stack.Pop();

        orderedValues[output] = values[index];
        orderedSubtreeSizes[output] = subtreeSizes[index];
        output++;

        var end = index + subtreeSizes[index];

        for (var child = index + 1; child < end; child += subtreeSizes[child])
          siblingGroup.Add(child);

        PushSiblingGroupInOrder();
      }

      return new AsyncPreorderArrayStore<TNode>(orderedValues, orderedSubtreeSizes);
    }
  }
}
