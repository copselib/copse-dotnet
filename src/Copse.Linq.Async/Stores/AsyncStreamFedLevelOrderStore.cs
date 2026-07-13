using Copse.Async;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Stores
{
  // A lazily created, incrementally built level-order capture FED BY AN
  // IAsyncLevelOrderStream: the fusion that lets a group stream become a replayable store
  // without ever synthesizing an intermediate visit stream. The stream already speaks the
  // store's positional contract -- group 0 is the roots, group j+1 the children of node j,
  // items arriving in level order -- so the parse is a bare cursor: append each item, charge it
  // to the group's owner, advance on group boundaries. No window, no queues, no per-visit
  // machinery (the FlatDecode family prices the windowed stream decoder this bypasses at 4-83x
  // the store decoder it enables). Cf. AsyncMemoizeLevelOrderBuffer, the same capture fed by
  // a visit stream, whose front cursor this store's group cursor replaces.
  //
  // The feed is created on the first grow call and disposed the moment it exhausts -- once
  // complete the stream (and the treenumerator it owns) is never touched again. ConsumeAsync
  // is the dispose-time completion hook (a no-op once complete): replays ride
  // AsyncDisposeActionTreenumerator, so a replay abandoned mid-drain finishes the capture and
  // releases the source deterministically (the Using idiom, as in the memo cluster).
  //
  // Single-threaded by contract, like every treenumerator in the library.
  //
  // Taxonomy (docs/STORE_FAMILY_REVIEW.md): level-order x growing x stream feed.
  internal sealed class AsyncStreamFedLevelOrderStore<TValue> : IAsyncLevelOrderStore<TValue>
  {
    public AsyncStreamFedLevelOrderStore(Func<IAsyncLevelOrderStream<TValue>> feedFactory)
    {
      _FeedFactory = feedFactory;
    }

    private readonly Func<IAsyncLevelOrderStream<TValue>> _FeedFactory;
    private IAsyncLevelOrderStream<TValue> _Feed;

    private readonly RefAppendOnlyList<TValue> _Values = new RefAppendOnlyList<TValue>();
    private readonly RefAppendOnlyList<int> _FirstChildIndices = new RefAppendOnlyList<int>();
    private readonly RefAppendOnlyList<int> _ChildCounts = new RefAppendOnlyList<int>();

    // The group the feed cursor is inside: group 0 is the roots, group j+1 node j's children --
    // so the current group's owner is _CurrentGroup - 1, and a span is final once the cursor
    // has moved past its group (or the feed exhausted).
    private int _CurrentGroup;
    private int _RootCount;

    // True once the feed has exhausted: the buffer is the whole tree and every span is closed.
    public bool Complete { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetValue(int index) => _Values[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstChildIndex(int parentIndex) => _FirstChildIndices[parentIndex];

    // Pull until root ordinal k exists (roots are buffer indices [0, rootCount)). False iff the
    // root frontier closed first: k is past the last root. Split along the buffered/pulling
    // line: an already-buffered answer is a plain read with no state machine.
    public ValueTask<bool> EnsureRootAvailableAsync(int k)
    {
      if (!Complete && _CurrentGroup == 0 && _RootCount <= k)
        return PullThenEnsureRootAvailableAsync(k);

      return new ValueTask<bool>(k < _RootCount);
    }

    private async ValueTask<bool> PullThenEnsureRootAvailableAsync(int k)
    {
      while (!Complete && _CurrentGroup == 0 && _RootCount <= k)
        await PullOneAsync().ConfigureAwait(false);

      return k < _RootCount;
    }

    // Pull until child ordinal k of the (already-buffered) parent exists. False iff the parent's
    // span closed first: k is past its last child. Children are served as they appear -- a span
    // need not close unless the answer is "no more".
    public ValueTask<bool> EnsureChildAvailableAsync(int parentIndex, int k)
    {
      if (!Complete && _CurrentGroup <= parentIndex + 1 && _ChildCounts[parentIndex] <= k)
        return PullThenEnsureChildAvailableAsync(parentIndex, k);

      return new ValueTask<bool>(k < _ChildCounts[parentIndex]);
    }

    private async ValueTask<bool> PullThenEnsureChildAvailableAsync(int parentIndex, int k)
    {
      while (!Complete && _CurrentGroup <= parentIndex + 1 && _ChildCounts[parentIndex] <= k)
        await PullOneAsync().ConfigureAwait(false);

      return k < _ChildCounts[parentIndex];
    }

    // Advance the parse by one item or one group boundary.
    private async ValueTask PullOneAsync()
    {
      if (_Feed == null)
        _Feed = _FeedFactory();

      var read = await _Feed.TryReadNextInGroupAsync().ConfigureAwait(false);

      if (read.HasValue)
      {
        Append(read.Value);
        return;
      }

      if (await _Feed.TryMoveToNextGroupAsync().ConfigureAwait(false))
        _CurrentGroup++;
      else
        await RetireFeedAsync().ConfigureAwait(false);
    }

    // Drive the feed to exhaustion: the buffer becomes the whole tree, every span closes, and
    // the source is retired. The bulk twin of PullOne -- same per-item logic with the guards
    // and the method call hoisted out of the loop -- because this is the abandoned-drain and
    // materialize hot path, where per-item overhead is the whole cost.
    public async ValueTask ConsumeAsync()
    {
      if (Complete)
        return;

      if (_Feed == null)
        _Feed = _FeedFactory();

      var feed = _Feed;

      while (true)
      {
        var read = await feed.TryReadNextInGroupAsync().ConfigureAwait(false);

        if (read.HasValue)
        {
          Append(read.Value);
          continue;
        }

        if (await feed.TryMoveToNextGroupAsync().ConfigureAwait(false))
        {
          _CurrentGroup++;
          continue;
        }

        await RetireFeedAsync().ConfigureAwait(false);
        return;
      }
    }

    private void Append(TValue value)
    {
      var index = _Values.Count;

      _Values.AddLast(value);
      _FirstChildIndices.AddLast(-1); // set when this node's first child arrives
      _ChildCounts.AddLast(0);

      if (_CurrentGroup == 0)
      {
        _RootCount++;
        return;
      }

      var owner = _CurrentGroup - 1;

      if (_ChildCounts[owner] == 0)
        _FirstChildIndices[owner] = index;

      _ChildCounts[owner]++;
    }

    private async ValueTask RetireFeedAsync()
    {
      Complete = true;
      await _Feed.DisposeAsync().ConfigureAwait(false);
      _Feed = null;
    }
  }
}
