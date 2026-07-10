using Copse.Core;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator over a level-order store, and the codegen source of truth
  /// for its sync twin: strip the <c>await</c> on the store's grow calls and it becomes the
  /// synchronous driver. The CROSS-ORDER member of the flat family's DFT pair -- children come from
  /// the store's contiguous child spans (child ordinal k is store index firstChildIndex + k) -- so
  /// descending is index chasing, O(depth). Grow calls are awaited so a store still capturing from
  /// an async feed fills just in time; GetFirstChildIndex/GetValue stay sync.
  /// </summary>
  public sealed class AsyncLevelOrderStoreDepthFirstTreenumerator<TValue, TStore>
    : AsyncTreenumeratorBase<TValue>
    where TStore : IAsyncLevelOrderStore<TValue>
  {
    public AsyncLevelOrderStoreDepthFirstTreenumerator(TStore store)
    {
      _Store = store;
      _Path = new RefSemiDeque<Level>();
    }

    // Non-readonly so interface calls on a struct store mutate it in place rather than a
    // defensive copy.
    private TStore _Store;

    // Root-to-current path, including SkipNode'd levels (kept resident, flagged, so their
    // children promote into their slot).
    private readonly RefSemiDeque<Level> _Path;

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
      public int NextChildOrdinal;
    }

    // NOT async, and neither are the helpers below: every store grow is PROBED, and the pull
    // stays ordinary method calls whenever the store answers inline. Only a genuinely pending
    // grow enters an async continuation -- see the fast-path probe idiom note in AsyncToSync.
    protected override ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
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

      var available = _Store.EnsureRootAvailableAsync(_RootsSeen);

      if (!available.IsCompletedSuccessfully)
        return AwaitThenMoveToNextRootNodeAsync(available);

      if (!available.Result)
        return new ValueTask<bool>(false);

      // Roots are the depth-0 prefix: ordinal and store index coincide.
      PushLevel(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

      return new ValueTask<bool>(true);
    }

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

    private ValueTask<bool> OnVisitingAsync()
    {
      var pushed = TryPushNextChildAsync();

      if (!pushed.IsCompletedSuccessfully)
        return AwaitPushThenBacktrackAsync(pushed);

      if (pushed.Result)
        return new ValueTask<bool>(true);

      return BacktrackAsync();
    }

    // Unwind finished levels and emit the next owed visit; see the sync twin.
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

    // THE SEAM, child-span edition: the active level's next child is child ordinal
    // NextChildOrdinal, at store index firstChildIndex + ordinal once the store has grown far
    // enough to prove it exists.
    //
    // The pre-probe reads use a copy of the top level: a pending grow resumes through a
    // continuation that RE-ENTERS this method (the re-issued grow is idempotent and answers
    // inline the second time), so nothing may mutate before the probe; the mutation re-acquires
    // the top by ref after it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<bool> TryPushNextChildAsync()
    {
      var top = _Path.GetLast();

      if (top.ChildrenDisabled)
        return new ValueTask<bool>(false);

      var ordinal = top.NextChildOrdinal;

      var available = _Store.EnsureChildAvailableAsync(top.NodeIndex, ordinal);

      if (!available.IsCompletedSuccessfully)
        return AwaitThenTryPushNextChildAsync(available);

      if (!available.Result)
        return new ValueTask<bool>(false);

      var childIndex = _Store.GetFirstChildIndex(top.NodeIndex) + ordinal;

      ref var topSlot = ref _Path.GetLast();

      topSlot.NextChildOrdinal++;

      var position = new NodePosition(ordinal, topSlot.Position.Depth + 1);

      PushLevel(childIndex, position);

      return new ValueTask<bool>(true);
    }

    // codegen: begin async-only
    //
    // The suspension continuations. Await the pending grow, then RE-ENTER the probing method:
    // the re-issued grow is idempotent ("grow until") and now answers inline, and the probing
    // methods mutate nothing before their probes. Re-entering Backtrack after a failed push IS
    // its loop's `continue`.
    private async ValueTask<bool> AwaitThenMoveToNextRootNodeAsync(ValueTask<bool> pendingGrow)
    {
      await pendingGrow.ConfigureAwait(false);

      return await MoveToNextRootNodeAsync().ConfigureAwait(false);
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
