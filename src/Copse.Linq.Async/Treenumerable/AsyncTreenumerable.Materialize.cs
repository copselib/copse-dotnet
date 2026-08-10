using Copse.Async;
using Copse.Async.Stores;
using Copse.Async.Treenumerables;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Stores;
using Copse.Linq.Async.Treenumerables;

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
    public static IAsyncTreenumerableBuffer<TValue> Materialize<TValue>(this IAsyncTreenumerable<TValue> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TValue> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TValue>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      return new AsyncTreenumerableBuffer<TValue>(
        AsyncTree.Lazy(firstDimension =>
          firstDimension == TreeTraversalStrategy.BreadthFirst
            ? DeferredLevelOrderCapture(source)
            : DeferredPreorderCapture(source)),
        nativeLayout: null); // decided by the first pull (the dimension dispatch above)
    }

    /// <summary>
    /// Materialize with a GUARANTEED capture layout, deferred: the returned buffer's
    /// native-replay dimension is <paramref name="strategy"/>, whatever the input -- the
    /// argument is never ignored -- but the O(n) construction is pinned to the first pull. The
    /// PIN lands NOW, because now is when it is free: a plain tree's capture layout is simply
    /// recorded; a live memo's capture is created for the requested dimension at this call
    /// (acquisition is the pin, zero nodes pulled), so an intervening consumer of a shared memo
    /// cannot pin it the other way. A buffer already in the layout is returned as-is (a capture
    /// is never re-captured); a mismatched or undecided one is TRANSPOSED -- from the buffer,
    /// never from the source (buffer traversal is effect-free by contract, so at-most-once
    /// holds), at the first pull, a NEW instance. A memo whose history had already pinned the
    /// other layout completes its pinned capture first (the one source enumeration), then
    /// transposes, all inside the first pull's settle. This stays the both-layouts recipe:
    /// materialize once, then materialize THAT in the other dimension. Contrast
    /// Consume(strategy), where the strategy is only a suggestion -- Materialize returns the
    /// buffer, so the layout IS the deliverable.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TValue> Materialize<TValue>(this IAsyncTreenumerable<TValue> source, TreeTraversalStrategy strategy)
    {
      var requestedLayout = strategy == TreeTraversalStrategy.DepthFirst ? BufferLayout.Preorder : BufferLayout.LevelOrder;

      if (source is IAsyncMemoizeTreenumerableBuffer<TValue> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TValue>(lazyBuffer, requestedLayout);

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
      {
        if (completedBuffer.NativeLayout == requestedLayout)
          return completedBuffer;

        return requestedLayout == BufferLayout.Preorder
          ? new AsyncTreenumerableBuffer<TValue>(DeferredPreorderCapture(completedBuffer), BufferLayout.Preorder)
          : new AsyncTreenumerableBuffer<TValue>(DeferredLevelOrderCapture(completedBuffer), BufferLayout.LevelOrder);
      }

      return requestedLayout == BufferLayout.Preorder
        ? new AsyncTreenumerableBuffer<TValue>(DeferredPreorderCapture(source), BufferLayout.Preorder)
        : new AsyncTreenumerableBuffer<TValue>(DeferredLevelOrderCapture(source), BufferLayout.LevelOrder);
    }

    /// <summary>
    /// The single-dimension upgrades, deferred like the composite: the capture layout is FORCED
    /// by the source's one affordable dimension (a depth-first-only source can only be consumed
    /// depth-first, so the capture is preorder) -- known at the call, reported by
    /// <c>NativeLayout</c> immediately, paid at the first pull. The same buffer probes apply: a
    /// narrow source that is secretly a capture is wrapped (memo) or returned as-is (buffer),
    /// never re-captured.
    /// </summary>
    public static IAsyncTreenumerableBuffer<TValue> Materialize<TValue>(this IAsyncDepthFirstTreenumerable<TValue> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TValue> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TValue>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      return new AsyncTreenumerableBuffer<TValue>(DeferredPreorderCapture(source), BufferLayout.Preorder);
    }

    public static IAsyncTreenumerableBuffer<TValue> Materialize<TValue>(this IAsyncBreadthFirstTreenumerable<TValue> source)
    {
      if (source is IAsyncMemoizeTreenumerableBuffer<TValue> lazyBuffer)
        return new AsyncMaterializeTreenumerable<TValue>(lazyBuffer, requestedLayout: null);

      if (source is IAsyncTreenumerableBuffer<TValue> completedBuffer)
        return completedBuffer;

      return new AsyncTreenumerableBuffer<TValue>(DeferredLevelOrderCapture(source), BufferLayout.LevelOrder);
    }

    // The deferral seam both layouts share (the LeaffixScan/Invert pattern): a lazy store whose
    // awaited build -- ONE capture walk of the source -- runs through the grow seam on the
    // first replay pull, both dimensions replaying from the completed store thereafter.
    private static IAsyncTreenumerable<TValue> DeferredPreorderCapture<TValue>(IAsyncDepthFirstTreenumerable<TValue> source)
      => new AsyncPreorderTreenumerable<TValue, AsyncLazyPreorderStore<TValue>>(
        new AsyncLazyPreorderStore<TValue>(() => AsyncPreorderCapture.CaptureFromAsync(source)));

    private static IAsyncTreenumerable<TValue> DeferredLevelOrderCapture<TValue>(IAsyncBreadthFirstTreenumerable<TValue> source)
      => new AsyncLevelOrderTreenumerable<TValue, AsyncLazyLevelOrderStore<TValue>>(
        new AsyncLazyLevelOrderStore<TValue>(() => AsyncLevelOrderCapture.CaptureFromAsync(source)));
  }
}
