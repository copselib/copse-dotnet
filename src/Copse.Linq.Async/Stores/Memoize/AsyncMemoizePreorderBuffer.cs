using Copse.Async.Stores;
using Copse.Core;
using Copse.Core.Async;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // The DFT dimension buffer of a memo: a lazily created, incrementally built preorder capture of the source --
  // values[i] plus subtreeSizes[i], node i's subtree spanning [i, i + subtreeSizes[i]) -- fed by
  // the source's own depth-first treenumerator and pulled only as far as some replay's frontier.
  // This is Materialize's open-stack construction (itself TreeSerializer.Parse's, paren deltas
  // become depth deltas) made resumable: each pull advances the feed to the next appended node
  // and suspends, leaving the open-parent stack in place.
  //
  // The feed is driven TraverseAll (eager-skip: consumer pruning is a replay-time view, never a
  // cache hole), created on first pull, and disposed the moment it exhausts -- once complete the
  // source is never touched again. A node is appended on its first VISITING visit (VisitCount 1,
  // the selector Materialize uses; in DFT it lands immediately after the scheduling visit, at the
  // same preorder position and depth).
  //
  // subtreeSizes[i] == 0 means node i's subtree is still OPEN (any closed size is >= 1); closes
  // backfill in place through RefAppendOnlyList's ref indexer. This gives replays an O(1)
  // closed-test without consulting the open stack.
  //
  // Single-threaded by contract, like every treenumerator in the library.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): preorder x growing x resumable visit-stream feed.
  internal sealed class AsyncMemoizePreorderBuffer<TValue> : IAsyncDisposable
  {
    public AsyncMemoizePreorderBuffer(Func<IAsyncTreenumerator<TValue>> feedFactory)
    {
      _FeedFactory = feedFactory;
    }

    private readonly Func<IAsyncTreenumerator<TValue>> _FeedFactory;
    private IAsyncTreenumerator<TValue> _Feed;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _SubtreeSizes = new RefAppendOnlyList<int>();

    // Indices of nodes whose subtree is still open, root-to-current -- a churning stack, so it
    // lives in a RefSemiDeque (contrast the monotonic buffers above).
    private readonly RefSemiDeque<int> _OpenParents = new RefSemiDeque<int>();

    private bool _Disposed;

    // Nodes buffered so far; a contiguous prefix of the full preorder stream.
    public int BufferedCount => _Values.Count;

    // True once the feed has exhausted: the buffer is the whole tree and every subtree is closed.
    public bool IsComplete { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Values[index];

    // Callers must have closed the subtree first (EnsureSubtreeClosed); 0 means still open.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    // Pull until the value at index exists. False iff the stream exhausted first (no such node).
    // Split along the buffered/pulling line: an already-buffered answer is a plain read with no
    // state machine, so replays over the captured region cost nothing async.
    public ValueTask<bool> EnsureBufferedAsync(int index)
    {
      if (!IsComplete && _Values.Count <= index)
        return PullThenEnsureBufferedAsync(index);

      return new ValueTask<bool>(index < _Values.Count);
    }

    private async ValueTask<bool> PullThenEnsureBufferedAsync(int index)
    {
      while (!IsComplete && _Values.Count <= index)
        await PullOneAsync().ConfigureAwait(false);

      return index < _Values.Count;
    }

    // Pull until node index's subtree closes (the next appended node lands at its depth or
    // shallower, or the stream ends) and return its size. The node itself must already be
    // buffered. This is the price of a replay skip-hop over an untraversed span: eager-skip
    // buffers the skipped subtree, lazily, only when hopped over.
    public ValueTask<int> EnsureSubtreeClosedAsync(int index)
    {
      if (!IsComplete && _SubtreeSizes[index] == 0)
        return PullThenEnsureSubtreeClosedAsync(index);

      return new ValueTask<int>(_SubtreeSizes[index]);
    }

    private async ValueTask<int> PullThenEnsureSubtreeClosedAsync(int index)
    {
      while (!IsComplete && _SubtreeSizes[index] == 0)
        await PullOneAsync().ConfigureAwait(false);

      return _SubtreeSizes[index];
    }

    // Drive the feed to exhaustion: the buffer becomes the whole tree, every span closes, and
    // the source is retired. The bulk twin of PullOne: same per-visit logic, but the guards and
    // the method call are hoisted out of the per-node loop -- this is Materialize's hot path,
    // where per-node overhead is the whole cost.
    public async ValueTask CompleteAsync()
    {
      if (IsComplete)
        return;

      if (_Disposed)
        throw new ObjectDisposedException(GetType().Name);

      if (_Feed == null)
        _Feed = _FeedFactory();

      var feed = _Feed;

      while (await feed.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
      {
        if (feed.VisitCount != 1)
          continue;

        var depth = feed.Position.Depth;

        while (_OpenParents.Count > depth)
          CloseOne();

        _OpenParents.AddLast(_Values.Count);
        _Values.AddLast(feed.Node);
        _SubtreeSizes.AddLast(0);
      }

      while (_OpenParents.Count > 0)
        CloseOne();

      IsComplete = true;
      await feed.DisposeAsync().ConfigureAwait(false);
      _Feed = null;
    }

    // Advance the feed to the next appended node, closing subtrees the depth deltas prove
    // finished; on exhaustion close everything, latch IsComplete, and drop the feed.
    private async ValueTask PullOneAsync()
    {
      if (_Disposed)
        throw new ObjectDisposedException(GetType().Name);

      if (_Feed == null)
        _Feed = _FeedFactory();

      while (await _Feed.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
      {
        if (_Feed.VisitCount != 1)
          continue;

        var depth = _Feed.Position.Depth;

        // Any still-open nodes at or below this depth are finished subtrees -- close them out.
        while (_OpenParents.Count > depth)
          CloseOne();

        _OpenParents.AddLast(_Values.Count);
        _Values.AddLast(_Feed.Node);
        _SubtreeSizes.AddLast(0); // backfilled by CloseOne when this node's subtree closes

        return;
      }

      while (_OpenParents.Count > 0)
        CloseOne();

      IsComplete = true;
      await _Feed.DisposeAsync().ConfigureAwait(false);
      _Feed = null;
    }

    private void CloseOne()
    {
      var closedIndex = _OpenParents.RemoveLast();
      _SubtreeSizes[closedIndex] = _Values.Count - closedIndex;
    }

    // Stops all future source consumption. Replays over the already-buffered region keep
    // working; one that needs to pull past the frontier gets ObjectDisposedException.
    public async ValueTask DisposeAsync()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      if (_Feed != null)
      {
        await _Feed.DisposeAsync().ConfigureAwait(false);
        _Feed = null;
      }
    }

    // Presents the buffer as the preorder store SPI for the native playback treenumerators. A
    // nested readonly struct so the playback's store calls specialize and inline -- the same
    // unboxed pattern as the engine's TChildEnumerator, and the same nested-Handle idiom as
    // the serializer's string stores: an adapter is meaningless without its owner.
    public readonly struct Handle : IAsyncPreorderStore<TValue>
    {
      public Handle(AsyncMemoizePreorderBuffer<TValue> buffer)
      {
        _Buffer = buffer;
      }

      private readonly AsyncMemoizePreorderBuffer<TValue> _Buffer;

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public ValueTask<bool> EnsureBufferedAsync(int index) => _Buffer.EnsureBufferedAsync(index);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public ValueTask<int> EnsureSubtreeClosedAsync(int index) => _Buffer.EnsureSubtreeClosedAsync(index);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public int GetSubtreeSize(int index) => _Buffer.GetSubtreeSize(index);

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public TValue GetValue(int index) => _Buffer.GetValue(index);
    }
  }
}
