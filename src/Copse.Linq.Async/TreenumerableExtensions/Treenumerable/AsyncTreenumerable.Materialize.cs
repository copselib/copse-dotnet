using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerables;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The re-traversable capture of the source's shape, DEFERRED (2026-08-10; eager until
    /// then): nothing is enumerated at the call. Construction is pinned to the first pull and
    /// runs through the lazy store's grow seam -- the LeaffixScan/Invert cost shape,
    /// capture(deferred-once) -- and THE FIRST CONSUMER PINS THE LAYOUT: depth-first-first
    /// captures preorder, breadth-first-first level-order (the lazy-Materialize law:
    /// construction is uniformly lazy; the pin is a commitment made at the earliest moment it
    /// is free, which for this overload is the first pull). No longer awaitable, so the Async
    /// suffix is gone. The source is enumerated AT MOST ONCE; both dimensions replay from the
    /// one capture; an unconsumed result holds exactly what the unconsumed pipeline already
    /// held, since nothing opens before the first pull.
    ///
    /// <para>Idempotent on a capture (probe order matters: the lazy interface derives from the
    /// completed one, so it is tested first): a live memo is wrapped so its one capture
    /// COMPLETES IN BULK at the first pull -- inheriting whatever layout the memo's own history
    /// pinned, and retiring the feed at the settle, which is what distinguishes the result from
    /// the memo itself; a completed buffer is returned as-is (re-capturing would copy every
    /// node for nothing). Disposal of the result is nothing after the settle and vacuously
    /// nothing before it.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Materialize<TNode>(this IAsyncTreenumerable<TNode> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TNode> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TNode>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TNode> completedBuffer)
        return completedBuffer;

      return new AsyncTreenumerableBuffer<TNode>(
        AsyncTree.Lazy(firstDimension =>
          firstDimension == TreeTraversalStrategy.BreadthFirst
            ? DeferredLevelOrderCapture(source)
            : DeferredPreorderCapture(source)),
        nativeLayout: null); // decided by the first pull (the dimension dispatch above)
    }

    /// <summary>
    /// Materialize with a GUARANTEED capture layout, deferred: the returned buffer's native
    /// layout is <paramref name="layout"/>, whatever the input -- the argument is never
    /// ignored -- but the O(n) construction is pinned to the first pull. The parameter speaks
    /// STORAGE vocabulary (<see cref="BufferLayout"/>'s naming rule: a strategy is how you
    /// WALK, a layout is how a capture is SHAPED -- and the layout is exactly this operator's
    /// deliverable; until 2026-08-10 it took a TreeTraversalStrategy and opened by converting
    /// it, the tell that it spoke the wrong vocabulary). The PIN lands NOW, because now is when
    /// it is free: a plain tree's capture layout is simply recorded; a live memo's capture is
    /// created for the layout's native dimension at this call (acquisition is the pin, zero
    /// nodes pulled), so an intervening consumer of a shared memo cannot pin it the other way.
    /// A buffer already in the layout is returned as-is (a capture is never re-captured); a
    /// mismatched or undecided one is TRANSPOSED -- from the buffer, never from the source
    /// (buffer traversal is effect-free by contract, so at-most-once holds), at the first pull,
    /// a NEW instance. A memo whose history had already pinned the other layout completes its
    /// pinned capture first (the one source enumeration), then transposes, all inside the first
    /// pull's settle. This stays the both-layouts recipe: materialize once, then materialize
    /// THAT in the other layout. Contrast Consume(strategy), which correctly keeps TRAVERSAL
    /// vocabulary -- Consume walks; Materialize shapes, and returns the buffer, so the layout
    /// IS the deliverable.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Materialize<TNode>(this IAsyncTreenumerable<TNode> source, BufferLayout layout)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TNode> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TNode>(lazyBuffer, layout);

      if (source is IAsyncTreenumerableBuffer<TNode> completedBuffer)
      {
        if (completedBuffer.NativeLayout == layout)
          return completedBuffer;

        return layout == BufferLayout.Preorder
          ? PreorderCaptureBuffer(completedBuffer)
          : LevelOrderCaptureBuffer(completedBuffer);
      }

      return layout == BufferLayout.Preorder
        ? PreorderCaptureBuffer(source)
        : LevelOrderCaptureBuffer(source);
    }

    /// <summary>
    /// The single-dimension upgrades, deferred like the composite: the capture layout is FORCED
    /// by the source's one affordable dimension (a depth-first-only source can only be consumed
    /// depth-first, so the capture is preorder) -- known at the call, reported by
    /// <c>NativeLayout</c> immediately, paid at the first pull. The same buffer probes apply: a
    /// narrow source that is secretly a capture is wrapped (memo) or returned as-is (buffer),
    /// never re-captured.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TNode> Materialize<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TNode> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TNode>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TNode> completedBuffer)
        return completedBuffer;

      return PreorderCaptureBuffer(source);
    }

    public static IAsyncTreenumerableBuffer<TNode> Materialize<TNode>(this IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TNode> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TNode>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TNode> completedBuffer)
        return completedBuffer;

      return LevelOrderCaptureBuffer(source);
    }

    // The deferral seam both layouts share (the LeaffixScan/Invert pattern): a lazy store whose
    // awaited build -- ONE capture walk of the source -- runs through the grow seam on the
    // first replay pull, both dimensions replaying from the completed store thereafter.
    // The declared-layout builders share ONE lazy store between the stream half and the
    // adjacency probes -- probes attach at construction, so the settle path (which would
    // re-capture the buffer's own capture from its visit stream) never runs for a
    // Materialize-built buffer. The organic overload cannot pre-attach (its layout is
    // decided by the first pull's dimension), so it keeps the settle.
    // The presize fast-path's Linq half (2026-08-16): a source that is secretly a completed
    // concrete buffer can promise its exact node count (the buffer's counted-source door), so
    // the capture allocates final arrays exactly and skips the chunked build buffer -- the
    // transpose path's ~2n transient drops to 1n. The door is a pure read; a source whose
    // count is not already known takes the uncounted path unchanged.
    private static ValueTask<AsyncPreorderArrayStore<TNode>> CapturePreorderAsync<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      if (source is AsyncTreenumerableBuffer<TNode> buffer)
      {
        var countResult = buffer.TryGetNodeCount();

        if (countResult.HasValue)
          return AsyncPreorderCapture.CaptureFromAsync(source, countResult.Value);
      }

      return AsyncPreorderCapture.CaptureFromAsync(source);
    }

    private static ValueTask<AsyncLevelOrderArrayStore<TNode>> CaptureLevelOrderAsync<TNode>(IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      if (source is AsyncTreenumerableBuffer<TNode> buffer)
      {
        var countResult = buffer.TryGetNodeCount();

        if (countResult.HasValue)
          return AsyncLevelOrderCapture.CaptureFromAsync(source, countResult.Value);
      }

      return AsyncLevelOrderCapture.CaptureFromAsync(source);
    }

    private static AsyncTreenumerableBuffer<TNode> PreorderCaptureBuffer<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
    {
      var lazyStore = new AsyncLazyPreorderStore<TNode>(() => CapturePreorderAsync(source));

      return new AsyncTreenumerableBuffer<TNode>(
        new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(lazyStore),
        BufferLayout.Preorder,
        new AsyncPreorderAdjacencyIndex<TNode, AsyncLazyPreorderStore<TNode>>(lazyStore));
    }

    private static AsyncTreenumerableBuffer<TNode> LevelOrderCaptureBuffer<TNode>(IAsyncBreadthFirstTreenumerable<TNode> source)
    {
      var lazyStore = new AsyncLazyLevelOrderStore<TNode>(() => CaptureLevelOrderAsync(source));

      return new AsyncTreenumerableBuffer<TNode>(
        new AsyncLevelOrderTreenumerable<TNode, AsyncLazyLevelOrderStore<TNode>>(lazyStore),
        BufferLayout.LevelOrder,
        new AsyncLevelOrderAdjacencyIndex<TNode, AsyncLazyLevelOrderStore<TNode>>(lazyStore));
    }

    private static IAsyncTreenumerable<TNode> DeferredPreorderCapture<TNode>(IAsyncDepthFirstTreenumerable<TNode> source)
      => new AsyncPreorderTreenumerable<TNode, AsyncLazyPreorderStore<TNode>>(
        new AsyncLazyPreorderStore<TNode>(() => CapturePreorderAsync(source)));

    private static IAsyncTreenumerable<TNode> DeferredLevelOrderCapture<TNode>(IAsyncBreadthFirstTreenumerable<TNode> source)
      => new AsyncLevelOrderTreenumerable<TNode, AsyncLazyLevelOrderStore<TNode>>(
        new AsyncLazyLevelOrderStore<TNode>(() => CaptureLevelOrderAsync(source)));
  }
}
