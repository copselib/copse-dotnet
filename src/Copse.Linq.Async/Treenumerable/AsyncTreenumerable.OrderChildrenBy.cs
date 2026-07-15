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
    /// The breadth-first-only source overload: ONE walk of the source, no intermediate capture.
    /// Sibling groups are contiguous in a level-order arrival and each level's ordering settles
    /// before the next level arrives, so the build streams the source straight into the ordered
    /// level-order encoding, buffering only the level in flight (O(width) beyond the O(n) result
    /// every OrderChildrenBy pays, disclosed by the buffer return type). The result's native
    /// layout is level-order -- breadth-first replays decode it directly.
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
        AsyncTree.Lazy(() => LevelOrderOrderChildrenBreadthFirstSource(source, keySelector, comparer, descending: false)), BufferLayout.LevelOrder);

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
        AsyncTree.Lazy(() => LevelOrderOrderChildrenBreadthFirstSource(source, keySelector, comparer, descending: true)), BufferLayout.LevelOrder);

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

    // Layout follows the source's arrival: depth-first sources build preorder arrays (matching
    // LeaffixScan's measured layout decision), the breadth-first-narrow arm builds level-order
    // arrays directly -- the layout its arrival order IS, and the one its consumer (necessarily
    // breadth-first-inclined) replays natively.
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

    private static IAsyncTreenumerable<TNode> LevelOrderOrderChildrenBreadthFirstSource<TNode, TKey>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      var ordered = new AsyncLazyLevelOrderStore<TNode>(
        () => BuildOrderedChildrenLevelOrderAsync(source, keySelector, comparer, descending));

      return new AsyncLevelOrderTreenumerable<TNode, AsyncLazyLevelOrderStore<TNode>>(ordered);
    }

    // The breadth-first-narrow build: ONE walk of the source, no intermediate capture. In a
    // level-order arrival every sibling group is contiguous, and no level-d node is visited
    // until level d has been fully scheduled -- so by the time level d+1 starts arriving, level
    // d's permutation is already settled. Buffering just the level currently being scheduled
    // (O(width) auxiliary) is therefore enough to emit the ordered LEVEL-ORDER encoding
    // directly: flush a level when the front cursor crosses into it, ordering its sibling
    // groups by their parents' ordered positions and stable-sorting each group by key, and
    // backfill each parent's child span through the chunked lists' ref indexer (the same
    // backfill idiom as the memo builders and capture factories).
    private static async ValueTask<AsyncLevelOrderArrayStore<TNode>> BuildOrderedChildrenLevelOrderAsync<TNode, TKey>(
      IAsyncBreadthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      var values = new RefAppendOnlyList<TNode>();
      var firstChildIndices = new RefAppendOnlyList<int>();
      var childCounts = new RefAppendOnlyList<int>();
      var rootCount = 0;

      // The level currently being scheduled: sibling groups in arrival (source-parent) order.
      // ParentLevelPosition is the parent's source position within ITS level (-1 for the root
      // group); SourceLevelStart is the group's first member's source position within THIS level.
      var pendingGroups = new List<(int ParentLevelPosition, int SourceLevelStart, List<TNode> Nodes, List<TKey> Keys)>();
      var pendingLevelArrivals = 0;

      // The most recently flushed level: where it landed in the store, its source-to-ordered
      // permutation, and its source size (which is how far the front travels before crossing).
      var flushedLevelStoreStart = 0;
      var flushedLevelPermutation = default(int[]);
      var flushedLevelSourceSize = 0;

      // The front: the node whose children are currently arriving, tracked as its source
      // position within its own level.
      var frontPositionInLevel = 0;
      var frontLevelRemaining = 0;

      void FlushPendingLevel()
      {
        var levelStoreStart = values.Count;
        var orderedPositionInLevel = 0;
        var levelPermutation = new int[pendingLevelArrivals];

        // Roots arrive as one parentless group and stay first; deeper levels emit their groups
        // in the ORDERED order of their parents, which the previous flush settled.
        var groupsInOrderedParentOrder = flushedLevelPermutation == null
          ? (IEnumerable<(int ParentLevelPosition, int SourceLevelStart, List<TNode> Nodes, List<TKey> Keys)>)pendingGroups
          : pendingGroups.OrderBy(group => flushedLevelPermutation[group.ParentLevelPosition]);

        foreach (var group in groupsInOrderedParentOrder)
        {
          if (group.ParentLevelPosition < 0)
          {
            rootCount = group.Nodes.Count;
          }
          else
          {
            var parentStoreIndex = flushedLevelStoreStart + flushedLevelPermutation[group.ParentLevelPosition];
            firstChildIndices[parentStoreIndex] = values.Count;
            childCounts[parentStoreIndex] = group.Nodes.Count;
          }

          var memberIndices = Enumerable.Range(0, group.Nodes.Count);
          var orderedMemberIndices = descending
            ? memberIndices.OrderByDescending(memberIndex => group.Keys[memberIndex], comparer)
            : memberIndices.OrderBy(memberIndex => group.Keys[memberIndex], comparer);

          foreach (var memberIndex in orderedMemberIndices)
          {
            levelPermutation[group.SourceLevelStart + memberIndex] = orderedPositionInLevel;
            orderedPositionInLevel++;

            values.AddLast(group.Nodes[memberIndex]);
            firstChildIndices.AddLast(-1); // backfilled when this node's children flush
            childCounts.AddLast(0);
          }
        }

        flushedLevelStoreStart = levelStoreStart;
        flushedLevelPermutation = levelPermutation;
        flushedLevelSourceSize = pendingLevelArrivals;

        pendingGroups.Clear();
        pendingLevelArrivals = 0;
      }

      var treenumerator = source.GetAsyncBreadthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            var parentLevelPosition = treenumerator.Position.Depth == 0 ? -1 : frontPositionInLevel;

            if (pendingGroups.Count == 0 || pendingGroups[pendingGroups.Count - 1].ParentLevelPosition != parentLevelPosition)
              pendingGroups.Add((parentLevelPosition, pendingLevelArrivals, new List<TNode>(), new List<TKey>()));

            var currentGroup = pendingGroups[pendingGroups.Count - 1];
            currentGroup.Nodes.Add(treenumerator.Node);
            currentGroup.Keys.Add(keySelector(treenumerator.ToNodeContext()));
            pendingLevelArrivals++;
          }
          else if (treenumerator.VisitCount == 1)
          {
            // The front crosses into a level exactly when that level has fully arrived (no
            // level-d node is visited before level d finishes scheduling) -- flush it then.
            if (frontLevelRemaining == 0)
            {
              FlushPendingLevel();
              frontLevelRemaining = flushedLevelSourceSize;
              frontPositionInLevel = 0;
            }
            else
            {
              frontPositionInLevel++;
            }

            frontLevelRemaining--;
          }
        }
      }

      return new AsyncLevelOrderArrayStore<TNode>(
        values.ToArray(), firstChildIndices.ToArray(), childCounts.ToArray(), rootCount);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildOrderedChildrenAsync<TNode, TKey>(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, TKey> keySelector,
      IComparer<TKey> comparer,
      bool descending)
    {
      // 1. Capture flat preorder arrays plus the keys as a preorder-parallel side channel --
      //    the capture factory's keyed overload exists for exactly this hook, and it evaluates
      //    the key once per node, during the walk, against the SOURCE context.
      var (capture, keys) = await AsyncPreorderCapture.CaptureFromAsync(source, keySelector).ConfigureAwait(false);

      // 2. Emit the ordered layout. Every sibling group (the roots, then each emitted node's
      //    children) is sorted by its cached keys -- OrderBy/OrderByDescending for guaranteed
      //    stability -- and pushed in REVERSE so the group pops in order. Each subtree keeps its
      //    size; only ordering changes.
      var count = capture.Count;
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

      for (var root = 0; root < count; root += capture.GetSubtreeSize(root))
        siblingGroup.Add(root);

      PushSiblingGroupInOrder();

      var output = 0;

      while (stack.Count > 0)
      {
        var index = stack.Pop();

        orderedValues[output] = capture.GetValue(index);
        orderedSubtreeSizes[output] = capture.GetSubtreeSize(index);
        output++;

        var end = index + capture.GetSubtreeSize(index);

        for (var child = index + 1; child < end; child += capture.GetSubtreeSize(child))
          siblingGroup.Add(child);

        PushSiblingGroupInOrder();
      }

      return new AsyncPreorderArrayStore<TNode>(orderedValues, orderedSubtreeSizes);
    }
  }
}
