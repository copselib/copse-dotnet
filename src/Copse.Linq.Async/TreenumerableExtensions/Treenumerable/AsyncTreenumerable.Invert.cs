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
    /// (Tree.Lazy). Depth-first-first captures into mirrored preorder arrays;
    /// breadth-first-first drains the streaming mirror into a completed level-order capture --
    /// native replay for the dimension that asked. Both arms share one cost shape: the whole
    /// build runs on the first replay pull. Either way the source is enumerated at most once
    /// and both dimensions replay from the one capture; the O(n) is disclosed by the buffer
    /// return type.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Invert<TNode>(this IAsyncTreenumerable<TNode> source)
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(firstDimension =>
          firstDimension == TreeTraversalStrategy.BreadthFirst
            ? LevelOrderMirror(source)
            : PreorderMirror(source)),
        nativeLayout: null); // decided by the first pull (the dimension dispatch above)

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
      => new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(() => PreorderMirror(source)), BufferLayout.Preorder);

    private static IAsyncTreenumerable<TNode> PreorderMirror<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var mirror = new AsyncLazyPreorderStore<TNode>(() => BuildMirrorAsync(source));

      return new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(mirror);
    }

    // The breadth-first-first mirror: the streaming mirror drained ONCE into a completed
    // level-order capture, replays served by the store decoders. The stream already emits the
    // mirror in the store's own encoding, so nothing decodes tiers into a visit stream just to
    // re-encode them: the first cut here composed the narrow Invert with Memoize, and that
    // visit-stream round trip benchmarked 2.1-2.7x slower than the preorder capture it replaced
    // (Invert Bft rows); the stream-shaped CaptureFrom keeps the direct encoding path.
    //
    // Build-on-first-pull, all at once -- the same cost shape as the preorder arm. This
    // replaced an incrementally-fed store (tier-by-tier laziness for partial drains) whose
    // Dispose completed the remaining capture anyway, so the laziness was only ever real for a
    // replay abandoned WITHOUT disposal -- a contract violation. One shape for both arms, no
    // dispose-time cost surprise, and the capture's own disposal releases the source's
    // treenumerator (and a Using source's resource) deterministically inside the build.
    private static IAsyncTreenumerable<TNode> LevelOrderMirror<TNode>(IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var mirror = new AsyncLazyLevelOrderStore<TNode>(
        () => AsyncLevelOrderCapture.CaptureFromAsync(
          new AsyncInvertedLevelOrderStream<TNode>(source.GetAsyncBreadthFirstTreenumerator())));

      return new AsyncLevelOrderTreenumerable<TNode, AsyncLazyLevelOrderStore<TNode>>(mirror);
    }

    private static async ValueTask<AsyncPreorderArrayStore<TNode>> BuildMirrorAsync<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      // The receiver sniff (the 2026-08-14 experiment's collapse), at the ACQUISITION seam
      // so every overload funnelling here gets it: ANY capture hands over its skeleton (the
      // concrete buffer's raw store, or the stance walk for every other capture -- no
      // second capture either way, no layout condition since Stage B's stance mirror
      // assigns its own numbering); everything else pays the one capture the mirror always
      // owed.
      if (source is IAsyncTreenumerableBuffer<TNode> buffer)
      {
        if (buffer is AsyncTreenumerableBuffer<TNode> concreteBuffer)
        {
          var (hasStore, store) = await concreteBuffer.TryGetPreorderStoreAsync().ConfigureAwait(false);

          if (hasStore)
            return MirrorEmit(store);
        }

        return await WalkerMirrorAsync(buffer).ConfigureAwait(false);
      }

      var capture = await AsyncPreorderCapture.CaptureFromAsync(source).ConfigureAwait(false);

      return MirrorEmit(capture);
    }

    // The mirror's emit over flat preorder arrays (value + subtree size per node). Pushing
    // roots/children in forward order makes them pop in reverse, which is exactly the
    // mirror's preorder. Each subtree keeps its size; only ordering changes. This zero-key
    // LIFO emit stays specialized to Invert (it has CI benchmark rows); the generalized
    // sort-each-group emission belongs to OrderChildrenBy.
    private static AsyncPreorderArrayStore<TNode> MirrorEmit<TNode>(AsyncPreorderArrayStore<TNode> capture)
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

      return new AsyncPreorderArrayStore<TNode>(mirroredValues, mirroredSubtreeSizes);
    }

    // The mirror in PURE STANCE VOCABULARY (Stage B's migration): one LIFO walk of doors +
    // steps + extract -- no handle arithmetic, no handle-space enumeration, no re-entry, no
    // sizes prepass. Pushing roots and children in forward order makes them pop in reverse
    // (the mirror's preorder); a close marker under each node's children fences its span,
    // so sizes fall out of the output cursor. The walk assumes nothing about the receiver's
    // handle space -- any walkable capture mirrors in place, whatever its layout. Receipt
    // for the ledger: zero new walker features needed.
    private static async ValueTask<AsyncPreorderArrayStore<TNode>> WalkerMirrorAsync<TNode>(IAsyncTreenumerableBuffer<TNode> buffer)
    {
      var mirroredValues = new List<TNode>();
      var mirroredSubtreeSizes = new List<int>();

      // CloseIndex < 0 marks an emit entry (the walker is live); otherwise the entry closes
      // the span opened at CloseIndex.
      var stack = new Stack<(AsyncTreeWalker<TNode, int> Walker, int CloseIndex)>();

      var rootStances = new List<AsyncTreeWalker<TNode, int>>();

      for (var rootIndex = 0;
        (await buffer.TryGetTreeWalkerAtRootIndexAsync(rootIndex).ConfigureAwait(false)).TryGetValue(out var rootStance);
        rootIndex++)
        rootStances.Add(rootStance);

      foreach (var rootStance in rootStances)
        stack.Push((rootStance, -1));

      while (stack.Count > 0)
      {
        var (stance, closeIndex) = stack.Pop();

        if (closeIndex >= 0)
        {
          mirroredSubtreeSizes[closeIndex] = mirroredValues.Count - closeIndex;
          continue;
        }

        var outputIndex = mirroredValues.Count;

        mirroredValues.Add(await stance.GetValueAsync().ConfigureAwait(false));
        mirroredSubtreeSizes.Add(0);

        stack.Push((default, outputIndex));

        for (var childIndex = 0;
          (await stance.MoveToChildAsync(childIndex).ConfigureAwait(false)).TryGetValue(out var child);
          childIndex++)
          stack.Push((child, -1));
      }

      return new AsyncPreorderArrayStore<TNode>(mirroredValues.ToArray(), mirroredSubtreeSizes.ToArray());
    }
  }
}
