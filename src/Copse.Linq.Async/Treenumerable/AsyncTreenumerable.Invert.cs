using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Stores;
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
    /// arrays. Deferred: construction is pinned to the first treenumerator acquisition
    /// (Tree.Lazy), and the awaited build runs once, on the first replay pull, through the
    /// lazy-built store's grow seam. The O(n) is disclosed by the buffer return type.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
      => DeferredMirror(source);

    /// <summary>
    /// The full-source overload (also the disambiguator for a source that is both breadth- and
    /// depth-first): the mirror's representation is pinned to the FIRST dimension pulled
    /// (Tree.Lazy). Depth-first-first captures into mirrored preorder arrays; breadth-first-first
    /// pins the streaming mirror into a lazily-growing level-order capture -- native replay for
    /// the dimension that asked, visits emerging tier by tier rather than after a full build, and
    /// a partial drain buffering only what it reached. Either way the source is enumerated at
    /// most once and both dimensions replay from the one capture; the O(n) is disclosed by the
    /// buffer return type.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncTreenumerable<TNode> source)
      => new AsyncCompletedTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(firstDimension =>
          firstDimension == TreeTraversalStrategy.BreadthFirst
            ? LevelOrderMirror(source)
            : PreorderMirror(source)));

    /// <summary>
    /// The buffer overload: a capture in hand makes the mirror's depth-first dimension affordable,
    /// so the mirror is a full citizen -- returned as a completed buffer (the mirror owns fresh
    /// arrays; there is no live feed, so the non-disposable base). Built once, on the first replay
    /// pull, by walking the capture's depth-first replay into mirrored preorder arrays; the
    /// original source is never re-enumerated.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncTreenumerableBuffer<TNode> source)
      => DeferredMirror(source);

    // The mirror for sources whose breadth-first arrival cannot be streamed (a depth-first-only
    // source) or whose capture is already paid (a buffer): construction pinned to the first
    // acquisition (Tree.Lazy), both dimensions served from mirrored preorder arrays. The
    // full-source overload dispatches on the first dimension instead -- see LevelOrderMirror.
    private static IAsyncTreenumerableBuffer<TNode> DeferredMirror<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
      => new AsyncCompletedTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderMirror(source)));

    private static IAsyncTreenumerable<TNode> PreorderMirror<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var mirror = new AsyncLazyBuiltPreorderStore<TNode>(() => BuildMirrorAsync(source));

      return new AsyncPreorderTreenumerable<TNode, AsyncLazyBuiltPreorderStore<TNode>>(mirror);
    }

    // The breadth-first-first mirror: the streaming mirror's tier output pinned into a lazily
    // growing level-order capture (AsyncStreamFedLevelOrderStore over
    // AsyncInvertedLevelOrderStream), replays served by the store decoders. The stream already
    // emits the mirror in the store's own encoding, so nothing decodes tiers into a visit
    // stream just to re-encode them: the first cut here composed the narrow Invert with
    // Memoize, and that visit-stream round trip benchmarked 2.1-2.7x slower than the preorder
    // capture it replaced (Invert Bft rows); the fused store keeps the same laziness -- visits
    // emerge tier by tier, partial drains buffer less -- without the round trip.
    //
    // Treenumerator disposal is the release point (the Using idiom): every replay's dispose
    // runs the capture-completing Consume (a no-op once complete), so a replay abandoned
    // mid-drain finishes the capture -- the same O(n) the preorder arm pays up front -- and
    // the store retires its feed, releasing the source's treenumerator deterministically
    // instead of holding it (and a Using source's resource) until GC.
    private static IAsyncTreenumerable<TNode> LevelOrderMirror<TNode>(IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var mirror = new AsyncStreamFedLevelOrderStore<TNode>(
        () => new AsyncInvertedLevelOrderStream<TNode>(source.GetAsyncBreadthFirstTreenumerator()));

      return new AsyncDelegatingTreenumerable<TNode>(
        () => new AsyncDisposeActionTreenumerator<TNode>(
          new AsyncLevelOrderStoreBreadthFirstTreenumerator<TNode, AsyncStreamFedLevelOrderStore<TNode>>(mirror),
          () => mirror.ConsumeAsync()),
        () => new AsyncDisposeActionTreenumerator<TNode>(
          new AsyncLevelOrderStoreDepthFirstTreenumerator<TNode, AsyncStreamFedLevelOrderStore<TNode>>(mirror),
          () => mirror.ConsumeAsync()));
    }

    private static async ValueTask<PreorderArrayStore<TNode>> BuildMirrorAsync<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      // 1. Capture flat preorder arrays (value + subtree size per node) from one awaited
      //    depth-first walk of the source -- the flat family's shared encode.
      var capture = await AsyncPreorderCapture.CaptureFromAsync(source).ConfigureAwait(false);

      // 2. Emit the mirror. Pushing roots/children in forward order makes them pop in reverse,
      //    which is exactly the mirror's preorder. Each subtree keeps its size; only ordering
      //    changes. This zero-key LIFO emit stays specialized to Invert (it has CI benchmark
      //    rows); the generalized sort-each-group emission belongs to OrderChildrenBy.
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
  }
}
