using Copse.Core;
using System;

namespace Copse.Linq.Treenumerators
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
  internal sealed class MemoizeDepthFirstBuffer<TValue> : IDisposable
  {
    public MemoizeDepthFirstBuffer(Func<ITreenumerator<TValue>> feedFactory)
    {
      _FeedFactory = feedFactory;
    }

    private readonly Func<ITreenumerator<TValue>> _FeedFactory;
    private ITreenumerator<TValue> _Feed;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _SubtreeSizes = new RefAppendOnlyList<int>();

    // Indices of nodes whose subtree is still open, root-to-current -- a churning stack, so it
    // lives in a RefSemiDeque (contrast the monotonic buffers above).
    private readonly RefSemiDeque<int> _OpenParents = new RefSemiDeque<int>();

    private bool _Disposed;

    // Nodes buffered so far; a contiguous prefix of the full preorder stream.
    public int BufferedCount => _Values.Count;

    // True once the feed has exhausted: the buffer is the whole tree and every subtree is closed.
    public bool Complete { get; private set; }

    public TValue GetValue(int index) => _Values[index];

    // Callers must have closed the subtree first (EnsureSubtreeClosed); 0 means still open.
    public int GetSubtreeSize(int index) => _SubtreeSizes[index];

    // Pull until the value at index exists. False iff the stream exhausted first (no such node).
    public bool EnsureBuffered(int index)
    {
      while (!Complete && _Values.Count <= index)
        PullOne();

      return index < _Values.Count;
    }

    // Pull until node index's subtree closes (the next appended node lands at its depth or
    // shallower, or the stream ends) and return its size. The node itself must already be
    // buffered. This is the price of a replay skip-hop over an untraversed span: eager-skip
    // buffers the skipped subtree, lazily, only when hopped over.
    public int EnsureSubtreeClosed(int index)
    {
      while (!Complete && _SubtreeSizes[index] == 0)
        PullOne();

      return _SubtreeSizes[index];
    }

    // Drive the feed to exhaustion: the buffer becomes the whole tree, every span closes, and
    // the source is retired. The bulk twin of PullOne: same per-visit logic, but the guards and
    // the method call are hoisted out of the per-node loop -- this is Materialize's hot path,
    // where per-node overhead is the whole cost.
    public void Consume()
    {
      if (Complete)
        return;

      if (_Disposed)
        throw new ObjectDisposedException(GetType().Name);

      if (_Feed == null)
        _Feed = _FeedFactory();

      var feed = _Feed;

      while (feed.MoveNext(NodeTraversalStrategies.TraverseAll))
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

      Complete = true;

      feed.Dispose();
      _Feed = null;
    }

    // Lazily enumerates the buffer indices of the roots -- the top-level spans, each hop filling
    // only far enough to close the previous root's subtree. The open-span dual of
    // PreorderTree.RootIndices.
    public System.Collections.Generic.IEnumerable<int> EnumerateRootIndices()
    {
      var index = 0;

      while (EnsureBuffered(index))
      {
        yield return index;
        index += EnsureSubtreeClosed(index);
      }
    }

    // Advance the feed to the next appended node, closing subtrees the depth deltas prove
    // finished; on exhaustion close everything, latch Complete, and drop the feed.
    private void PullOne()
    {
      if (_Disposed)
        throw new ObjectDisposedException(GetType().Name);

      if (_Feed == null)
        _Feed = _FeedFactory();

      while (_Feed.MoveNext(NodeTraversalStrategies.TraverseAll))
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

      Complete = true;

      _Feed.Dispose();
      _Feed = null;
    }

    private void CloseOne()
    {
      var closedIndex = _OpenParents.RemoveLast();
      _SubtreeSizes[closedIndex] = _Values.Count - closedIndex;
    }

    // Stops all future source consumption. Replays over the already-buffered region keep
    // working; one that needs to pull past the frontier gets ObjectDisposedException.
    public void Dispose()
    {
      if (_Disposed)
        return;

      _Disposed = true;

      _Feed?.Dispose();
      _Feed = null;
    }
  }
}
