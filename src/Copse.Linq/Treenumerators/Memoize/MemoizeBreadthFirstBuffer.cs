using Copse.Core;
using System;

namespace Copse.Linq.Treenumerators
{
  // The BFT dimension buffer of a memo: a lazily created, incrementally built LEVEL-ORDER capture
  // of the source -- values in arrival order (BFT scheduling order IS level order) plus each
  // node's child span (firstChildIndex + childCount, children of one node are contiguous in level
  // order) -- fed by the source's own breadth-first treenumerator and pulled only as far as some
  // replay's frontier. The structural dual of MemoizeDepthFirstBuffer, LOUDS-adjacent where that
  // one is balanced-parentheses-adjacent.
  //
  // The parse state is a single monotonic cursor. BFT visits nodes in level order -- the same
  // order they were scheduled, hence the same order they sit in the buffer -- so _FrontIndex
  // (advanced on each VisitCount-1 visiting visit) is always the buffer index of the node the
  // feed is currently visiting. And BFT schedules a node's children only while that node is being
  // visited (the parent-visit-between-child-schedules invariant), so every scheduled non-root's
  // parent is simply _FrontIndex: no stack, no search, no backtracking. Roots are the depth-0
  // prefix, all scheduled before the first visiting visit (the root frontier).
  //
  // Closure is a cursor comparison, not a stored sentinel: node i can gain children only while
  // it is the front, so its child span is final once _FrontIndex > i (or the feed exhausted).
  // childCount == 0 is a legitimate final value (leaves), which is why no in-band sentinel like
  // the DFT buffer's subtreeSizes-0 exists. A span does NOT have to close to be served from:
  // child k is available the moment childCount exceeds k, so replays consume children of a
  // still-open span as they appear -- only "no more children" waits for closure.
  //
  // The feed is driven TraverseAll (eager-skip: consumer pruning is a replay-time view, never a
  // cache hole), created on first pull, and disposed the moment it exhausts -- once complete the
  // source is never touched again.
  //
  // Single-threaded by contract, like every treenumerator in the library.
  internal sealed class MemoizeBreadthFirstBuffer<TValue> : IDisposable
  {
    public MemoizeBreadthFirstBuffer(Func<ITreenumerator<TValue>> feedFactory)
    {
      _FeedFactory = feedFactory;
    }

    private readonly Func<ITreenumerator<TValue>> _FeedFactory;
    private ITreenumerator<TValue> _Feed;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _FirstChildIndices = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _ChildCounts = new RefAppendOnlyList<int>();

    // Buffer index of the node the feed is currently visiting; -1 while the root frontier is
    // still being scheduled (no visiting visit yet).
    private int _FrontIndex = -1;

    // Number of depth-0 nodes appended. Final once the first visiting visit arrives (BFT
    // schedules the entire root frontier before visiting anything) or the feed exhausts.
    private int _RootCount;

    private bool _Disposed;

    // Nodes buffered so far; a contiguous prefix of the full level-order stream.
    public int BufferedCount => _Values.Count;

    // True once the feed has exhausted: the buffer is the whole tree and every span is closed.
    public bool Complete { get; private set; }

    public TValue GetValue(int index) => _Values[index];

    public int GetFirstChildIndex(int index) => _FirstChildIndices[index];

    public int GetChildCount(int index) => _ChildCounts[index];

    // Pull until root ordinal k exists (roots are buffer indices [0, rootCount)). False iff the
    // root frontier closed first: k is past the last root.
    public bool EnsureRootAvailable(int k)
    {
      while (!Complete && _FrontIndex < 0 && _RootCount <= k)
        PullOne();

      return k < _RootCount;
    }

    // Pull until child ordinal k of the (already-buffered) parent exists. False iff the parent's
    // span closed first: k is past its last child. Children are served as they appear -- a span
    // need not close unless the answer is "no more".
    public bool EnsureChildAvailable(int parentIndex, int k)
    {
      while (!Complete && _FrontIndex <= parentIndex && _ChildCounts[parentIndex] <= k)
        PullOne();

      return k < _ChildCounts[parentIndex];
    }

    // Drive the feed to exhaustion: the buffer becomes the whole tree, every span closes, and
    // the source is retired. The bulk twin of PullOne: same per-visit logic, but the guards and
    // the method call are hoisted out of the per-visit loop -- this is Materialize's hot path,
    // where per-visit overhead is the whole cost.
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
        if (feed.Mode == TreenumeratorMode.SchedulingNode)
        {
          var index = _Values.Count;

          _Values.AddLast(feed.Node);
          _FirstChildIndices.AddLast(-1); // set when this node's first child arrives
          _ChildCounts.AddLast(0);

          if (feed.Position.Depth == 0)
          {
            _RootCount++;
          }
          else
          {
            ref var frontChildCount = ref _ChildCounts[_FrontIndex];

            if (frontChildCount == 0)
              _FirstChildIndices[_FrontIndex] = index;

            frontChildCount++;
          }
        }
        else if (feed.VisitCount == 1)
        {
          _FrontIndex++;
        }
      }

      Complete = true;

      feed.Dispose();
      _Feed = null;
    }

    // Process one feed visit. Scheduling visits append (wiring the child into _FrontIndex's
    // span); each node's first visiting visit advances the front; revisits are structural
    // no-ops. On exhaustion latch Complete and drop the feed. Per-visit granularity keeps the
    // Ensure loops from over-pulling the source past their own condition.
    private void PullOne()
    {
      if (_Disposed)
        throw new ObjectDisposedException(GetType().Name);

      if (_Feed == null)
        _Feed = _FeedFactory();

      if (!_Feed.MoveNext(NodeTraversalStrategies.TraverseAll))
      {
        Complete = true;

        _Feed.Dispose();
        _Feed = null;

        return;
      }

      if (_Feed.Mode == TreenumeratorMode.SchedulingNode)
      {
        var index = _Values.Count;

        _Values.AddLast(_Feed.Node);
        _FirstChildIndices.AddLast(-1); // set when this node's first child arrives
        _ChildCounts.AddLast(0);

        if (_Feed.Position.Depth == 0)
        {
          _RootCount++;
        }
        else
        {
          ref var frontChildCount = ref _ChildCounts[_FrontIndex];

          if (frontChildCount == 0)
            _FirstChildIndices[_FrontIndex] = index;

          frontChildCount++;
        }
      }
      else if (_Feed.VisitCount == 1)
      {
        _FrontIndex++;
      }
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
