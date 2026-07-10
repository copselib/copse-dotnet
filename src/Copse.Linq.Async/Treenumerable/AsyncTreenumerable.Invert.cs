using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Treenumerators;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// Mirror: reverse the order of every node's children (and the roots). Two regimes, by what
    /// the source can afford (see TRAVERSAL_DIMENSION_SPLIT.md), mirroring the sync operator:
    /// a breadth-first-ONLY source streams its mirror in O(width) and stays narrow; anything
    /// else captures and returns a completed <see cref="IAsyncTreenumerableBuffer{TValue}"/>.
    ///
    /// <para>This overload is the streaming regime: reversing every sibling group reverses each
    /// level end-to-end, so no capture is needed and the result stays a narrow breadth-first
    /// treenumerable.</para>
    /// </summary>
    public static IAsyncBreadthFirstTreenumerable<TNode> Invert<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
      => new AsyncLevelOrderStreamTreenumerable<TNode, AsyncInvertedLevelOrderStream<TNode>>(
        () => new AsyncInvertedLevelOrderStream<TNode>(source.GetAsyncBreadthFirstTreenumerator()));

    /// <summary>
    /// The depth-first-only mirror cannot stream (the mirror owes the original's LAST child right
    /// after the root), so it captures: one awaited depth-first walk into mirrored preorder
    /// arrays, deferred to the first replay pull (async cannot defer inside a sync-signature
    /// treenumerator factory, so the deferral rides the lazy-built store's grow seam -- one step
    /// LAZIER than the sync twin, which captures at call time). The O(n) is disclosed by the
    /// buffer return type.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => DeferredMirror(source);

    /// <summary>
    /// The full-source convenience overload (also the disambiguator for a source that is both
    /// breadth- and depth-first): capture, then serve the mirror from the capture.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncTreenumerable<TNode> source)
      => DeferredMirror(source);

    /// <summary>
    /// The buffer overload: a capture in hand makes the mirror's depth-first dimension affordable,
    /// so the mirror is a full citizen -- returned as a completed buffer (the mirror owns fresh
    /// arrays; there is no live feed, so the non-disposable base). Built once, on the first replay
    /// pull, by walking the capture's depth-first replay into mirrored preorder arrays; the
    /// original source is never re-enumerated.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncTreenumerableBuffer<TNode> source)
      => DeferredMirror(source);

    // The mirror's construction is pinned to the FIRST dimension pulled (Tree.Lazy): the
    // capture's layout is a representation choice the first consumer should get to make. Today
    // both dimensions get the preorder layout (breadth-first rides it cross-order); when a
    // level-order array store exists, the breadth-first arm becomes a level-order mirror --
    // native replay for the dimension that asked -- without touching this shape.
    private static IAsyncTreenumerableBuffer<TNode> DeferredMirror<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
      => new AsyncCompletedTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(firstDimension => PreorderMirror(source)));

    private static IAsyncTreenumerable<TNode> PreorderMirror<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var mirror = new AsyncLazyBuiltPreorderStore<TNode>(() => BuildMirrorAsync(source));

      return new AsyncPreorderTreenumerable<TNode, AsyncLazyBuiltPreorderStore<TNode>>(mirror);
    }

    private static async ValueTask<PreorderArrayStore<TNode>> BuildMirrorAsync<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      // 1. Capture flat preorder arrays (value + subtree size per node) from one awaited
      //    depth-first walk of the source.
      var values = new List<TNode>();
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
          subtreeSizes.Add(0);
        }
      }

      while (open.Count > 0)
      {
        var closed = open.Pop();
        subtreeSizes[closed] = values.Count - closed;
      }

      // 2. Emit the mirror. Pushing roots/children in forward order makes them pop in reverse,
      //    which is exactly the mirror's preorder. Each subtree keeps its size; only ordering
      //    changes.
      var count = values.Count;
      var mirroredValues = new TNode[count];
      var mirroredSubtreeSizes = new int[count];
      var stack = new Stack<int>();

      for (var root = 0; root < count; root += subtreeSizes[root])
        stack.Push(root);

      var output = 0;

      while (stack.Count > 0)
      {
        var index = stack.Pop();

        mirroredValues[output] = values[index];
        mirroredSubtreeSizes[output] = subtreeSizes[index];
        output++;

        var end = index + subtreeSizes[index];

        for (var child = index + 1; child < end; child += subtreeSizes[child])
          stack.Push(child);
      }

      return new PreorderArrayStore<TNode>(mirroredValues, mirroredSubtreeSizes);
    }
  }
}
