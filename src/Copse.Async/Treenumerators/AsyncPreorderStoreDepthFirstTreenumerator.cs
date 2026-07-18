using Copse.Async.Stores;
using Copse.Core;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator over a preorder store, and the codegen source of truth
  /// for its sync twin (Copse.Treenumerators.PreorderStoreDepthFirstTreenumerator): strip the
  /// <c>await</c> on the store's grow calls and it becomes the synchronous driver. Native playback
  /// for the flat family from span arithmetic -- a node's first child is the next store index, a
  /// child's next sibling is the child's index plus its (ensured-closed) subtree size -- with the
  /// grow calls (<c>EnsureBufferedAsync</c>/<c>EnsureSubtreeClosedAsync</c>) PROBED so a store
  /// still capturing from an async feed fills just in time while an already-buffered answer
  /// costs no state machine at all (the fast-path probe idiom -- see AsyncToSync).
  /// GetValue/GetSubtreeSize stay sync.
  /// </summary>
  public sealed class AsyncPreorderStoreDepthFirstTreenumerator<TValue, TStore>
    : AsyncTreenumeratorBase<TValue>
    where TStore : IAsyncPreorderStore<TValue>
  {
    public AsyncPreorderStoreDepthFirstTreenumerator(TStore store)
    {
      _Store = store;
      _Path = new RefSemiDeque<Level>();
    }

    // Non-readonly so interface calls on a struct store mutate it in place rather than a
    // defensive copy (the same reasoning as the engine's _Path field).
    private TStore _Store;

    // Root-to-current path, including SkipNode'd levels (kept resident, flagged, so their
    // children promote into their slot). The top level's raw depth is always _Path.Count - 1.
    private readonly RefSemiDeque<Level> _Path;

    private int _LastRootIndex = -1;
    private int _RootsSeen;
    private bool _RootsFinished;

    // Raw depth of the most recently emitted VisitingNode; lets a backtracked-to level tell
    // whether it already took its return visit (see Backtrack).
    private int _DepthOfLastVisitedNode = -1;

    private struct Level
    {
      public int NodeIndex;
      public NodePosition Position;
      public int VisitCount;
      public bool Skipped;           // SkipNode'd: no visits, resident only to promote children.
      public bool ChildrenDisabled;  // SkipDescendants/SkipSiblings: yield no more children.
      public int LastChildIndex;     // -1 = no child scheduled yet.
      public int NextSiblingIndex;
    }

    // NOT async, and neither are the helpers below: every store grow is PROBED, and the pull
    // stays ordinary method calls whenever the store answers inline (a completed capture always
    // does; a growing one does whenever the answer is already buffered). Only a genuinely
    // pending grow enters an async continuation -- see the fast-path idiom note in AsyncToSync.
    protected override ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Nothing descended yet: schedule the next root (the first move, or the gap between roots).
      if (_Path.Count == 0)
        return MoveToNextRootNodeAsync();

      // The strategy applies to the node just scheduled; visiting nodes ignore it.
      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnSchedulingAsync(nodeTraversalStrategies);

      return OnVisitingAsync();
    }

    private ValueTask<bool> MoveToNextRootNodeAsync()
    {
      if (_RootsFinished)
        return new ValueTask<bool>(false);

      int candidate;

      if (_LastRootIndex < 0)
      {
        candidate = 0;
      }
      else
      {
        var closed = _Store.EnsureSubtreeClosedAsync(_LastRootIndex);

        if (!closed.IsCompletedSuccessfully)
          return AwaitThenMoveToNextRootNodeAsync(closed);

        candidate = _LastRootIndex + closed.Result;
      }

      var buffered = _Store.EnsureBufferedAsync(candidate);

      if (!buffered.IsCompletedSuccessfully)
        return AwaitThenMoveToNextRootNodeAsync(buffered);

      if (!buffered.Result)
      {
        _RootsFinished = true;
        return new ValueTask<bool>(false);
      }

      _LastRootIndex = candidate;

      PushLevel(candidate, new NodePosition(_RootsSeen++, 0));

      return new ValueTask<bool>(true);
    }

    // Apply the consumer's strategy to the node just scheduled, then emit its first visit (or
    // move on if it is skipped/pruned).
    private ValueTask<bool> OnSchedulingAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (SkipRemainingSiblings())
          _RootsFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an
      // all-bits test), so it must be checked first -- otherwise it would route into the SkipNode
      // promotion path and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return BacktrackAsync();

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.GetLast().Skipped = true;

        var pushed = TryPushNextChildAsync();

        if (!pushed.IsCompletedSuccessfully)
          return AwaitPushThenBacktrackAsync(pushed);

        if (pushed.Result)
          return new ValueTask<bool>(true);

        // No children to promote: a childless SkipNode'd node emits nothing.
        return BacktrackAsync();
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.GetLast().ChildrenDisabled = true;

      // Accept (TraverseAll, or the SkipDescendants fall-through): emit the node's first visit.
      TakeNextVisit();

      return new ValueTask<bool>(true);
    }

    // A VisitingNode was just emitted: descend into its next child, or backtrack if it has none
    // left.
    private ValueTask<bool> OnVisitingAsync()
    {
      var pushed = TryPushNextChildAsync();

      if (!pushed.IsCompletedSuccessfully)
        return AwaitPushThenBacktrackAsync(pushed);

      if (pushed.Result)
        return new ValueTask<bool>(true);

      return BacktrackAsync();
    }

    // Unwind finished levels and emit the next owed visit. Each iteration pops one exhausted
    // level and decides what the level we returned to owes: descend into its next child (a
    // skipped level, or one whose return visit was already the last visit emitted), or re-emit
    // the accepted node owed its between/after-children visit.
    private ValueTask<bool> BacktrackAsync()
    {
      while (true)
      {
        _Path.RemoveLast();

        if (_Path.Count == 0)
          return MoveToNextRootNodeAsync();

        // A copy (not a ref): a pending push resumes through a continuation that re-enters this
        // method, so nothing here may have mutated the level.
        var top = _Path.GetLast();

        if (top.Skipped || top.Position.Depth == _DepthOfLastVisitedNode)
        {
          var pushed = TryPushNextChildAsync();

          if (!pushed.IsCompletedSuccessfully)
            return AwaitPushThenBacktrackAsync(pushed);

          if (pushed.Result)
            return new ValueTask<bool>(true);

          continue;
        }

        TakeNextVisit();

        return new ValueTask<bool>(true);
      }
    }

    // THE SEAM, span-arithmetic edition: the active level's next child is the next store index
    // (first child) or the previous child's index plus its ensured-closed subtree size (next
    // sibling); it belongs to this level while the level's span is still open (everything
    // appended past a still-open node lies inside it) or contains the candidate.
    //
    // The pre-probe reads use a copy of the top level: a pending grow resumes through a
    // continuation that RE-ENTERS this method (the re-issued grow is idempotent and answers
    // inline the second time), so nothing may mutate before the probes; the mutation re-acquires
    // the top by ref after them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<bool> TryPushNextChildAsync()
    {
      var top = _Path.GetLast();

      if (top.ChildrenDisabled)
        return new ValueTask<bool>(false);

      int candidate;

      if (top.LastChildIndex < 0)
      {
        candidate = top.NodeIndex + 1;
      }
      else
      {
        var closed = _Store.EnsureSubtreeClosedAsync(top.LastChildIndex);

        if (!closed.IsCompletedSuccessfully)
          return AwaitThenTryPushNextChildAsync(closed);

        candidate = top.LastChildIndex + closed.Result;
      }

      var buffered = _Store.EnsureBufferedAsync(candidate);

      if (!buffered.IsCompletedSuccessfully)
        return AwaitThenTryPushNextChildAsync(buffered);

      if (!buffered.Result)
        return new ValueTask<bool>(false);

      var parentSubtreeSize = _Store.GetSubtreeSize(top.NodeIndex);

      if (parentSubtreeSize != 0 && candidate >= top.NodeIndex + parentSubtreeSize)
        return new ValueTask<bool>(false);

      ref var topSlot = ref _Path.GetLast();

      topSlot.LastChildIndex = candidate;

      var position = new NodePosition(topSlot.NextSiblingIndex++, topSlot.Position.Depth + 1);

      PushLevel(candidate, position);

      return new ValueTask<bool>(true);
    }

    // codegen: begin async-only
    //
    // The suspension continuations. Await the pending grow, then RE-ENTER the probing method:
    // the re-issued grow is idempotent ("grow until") and now answers inline, and the probing
    // methods mutate nothing before their probes, so re-entry replays a read-only prefix.
    // Re-entering Backtrack after a failed push IS its loop's `continue` -- the next iteration
    // pops the level whose push just came up empty.
    private async ValueTask<bool> AwaitThenMoveToNextRootNodeAsync(ValueTask<int> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await MoveToNextRootNodeAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenMoveToNextRootNodeAsync(ValueTask<bool> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await MoveToNextRootNodeAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenTryPushNextChildAsync(ValueTask<int> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await TryPushNextChildAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitThenTryPushNextChildAsync(ValueTask<bool> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await TryPushNextChildAsync().ConfigureAwait(false);
    }

    private async ValueTask<bool> AwaitPushThenBacktrackAsync(ValueTask<bool> pendingPush)
    {
      if (await pendingPush.ConfigureAwait(false))
        return true;

      return await BacktrackAsync().ConfigureAwait(false);
    }
    // codegen: end async-only

    // Schedule a node as a new level and publish its scheduling visit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushLevel(int nodeIndex, NodePosition position)
    {
      _Path.AddLast(new Level
      {
        NodeIndex = nodeIndex,
        Position = position,
        LastChildIndex = -1,
      });

      Mode = TreenumeratorMode.SchedulingNode;
      Node = _Store.GetValue(nodeIndex);
      VisitCount = 0;
      Position = position;
    }

    // Emit the active accepted node's next visit (its first, or a between/after-children return
    // visit).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TakeNextVisit()
    {
      ref var top = ref _Path.GetLast();

      top.VisitCount++;
      _DepthOfLastVisitedNode = top.Position.Depth;

      Mode = TreenumeratorMode.VisitingNode;
      Node = _Store.GetValue(top.NodeIndex);
      VisitCount = top.VisitCount;
      Position = top.Position;
    }

    // SkipSiblings: silence every level that could still yield an effective sibling of the
    // just-scheduled node -- its skipped ancestors up through its nearest accepted one. No
    // accepted ancestor means the node is an effective root: silence everything below and tell
    // the driver to end root enumeration.
    private bool SkipRemainingSiblings()
    {
      var wasEffectiveRoot = true;

      for (int i = 1; i < _Path.Count; i++)
      {
        ref var level = ref _Path.GetFromBack(i);

        level.ChildrenDisabled = true;

        if (!level.Skipped)
        {
          wasEffectiveRoot = false;
          break;
        }
      }

      return wasEffectiveRoot;
    }
  }
}
