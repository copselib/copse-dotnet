using Copse.Core;
using Copse.Core.Async;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Copse.Async.Treenumerators
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator over a forward-only preorder stream, in the <b>direct
  /// style</b>: the natural inlined control flow (OnScheduling / OnVisiting / Backtrack /
  /// TryPushNextChild), with <c>await</c> at the one I/O seam -- the stream read in
  /// TryEnsureLookaheadAtOrAbove. O(depth) resident state (the root-to-current path plus the
  /// lookahead slot); skips are lazy discards through <see cref="IAsyncPreorderStream{TValue}"/>'s
  /// TrySkipToDepth, which reads I/O without materializing values.
  ///
  /// <para><b>This is the single source of truth.</b> Strip the <c>await</c>s and it collapses to
  /// the synchronous <c>Copse.Treenumerators.PreorderStreamDepthFirstTreenumerator</c> (the checked-in
  /// <c>.g.cs</c> twin); the struct-return read seam is what makes the async form legal (out params
  /// cannot cross an await) at proven perf parity with the retired out-style stream.</para>
  /// </summary>
  public sealed class AsyncPreorderStreamDepthFirstTreenumerator<TValue, TStream>
    : IAsyncTreenumerator<TValue>
    where TStream : IAsyncPreorderStream<TValue>
  {
    public AsyncPreorderStreamDepthFirstTreenumerator(TStream stream)
    {
      _Stream = stream;
      _Path = new RefSemiDeque<Level>();
    }

    // Non-readonly so interface calls on a struct stream mutate it in place rather than a
    // defensive copy.
    private TStream _Stream;

    // Root-to-current path, including SkipNode'd levels (kept resident, flagged, so their
    // children promote into their slot).
    private readonly RefSemiDeque<Level> _Path;

    // One-token lookahead: the next streamed node, not yet claimed by any level.
    private bool _HasLookahead;
    private TValue _LookaheadValue;
    private int _LookaheadDepth;
    private bool _StreamExhausted;

    private int _RootsSeen;
    private bool _RootsFinished;

    // Raw depth of the most recently emitted VisitingNode; lets a backtracked-to level tell
    // whether it already took its return visit (see Backtrack).
    private int _DepthOfLastVisitedNode = -1;

    private bool _Finished;

    public TValue Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    private struct Level
    {
      public TValue Value;
      public NodePosition Position;
      public int VisitCount;
      public bool Skipped;           // SkipNode'd: no visits, resident only to promote children.
      public bool ChildrenDisabled;  // SkipDescendants/SkipSiblings: yield no more children.
      public int NextSiblingIndex;
    }

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      var moved = await OnMoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.Count == 0)
        return MoveToNextRootNodeAsync();

      // The strategy applies to the node just scheduled; visiting nodes ignore it.
      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnSchedulingAsync(nodeTraversalStrategies);

      return OnVisitingAsync();
    }

    private async ValueTask<bool> MoveToNextRootNodeAsync()
    {
      if (_RootsFinished)
        return false;

      // Roots are the forest's children: the same lookahead logic with the virtual forest
      // level at depth -1 (any remaining deeper content is a pruned ex-root's subtree).
      if (!await TryEnsureLookaheadAtOrAboveAsync(0).ConfigureAwait(false))
        return false;

      var value = _LookaheadValue;
      _HasLookahead = false;

      PushLevel(value, new NodePosition(_RootsSeen++, 0));

      return true;
    }

    private async ValueTask<bool> OnSchedulingAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (SkipRemainingSiblings())
          _RootsFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an
      // all-bits test), so it must be checked first -- otherwise it would route into the SkipNode
      // promotion path and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return await BacktrackAsync().ConfigureAwait(false);

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.GetLast().Skipped = true;

        if (await TryPushNextChildAsync().ConfigureAwait(false))
          return true;

        // No children to promote: a childless SkipNode'd node emits nothing.
        return await BacktrackAsync().ConfigureAwait(false);
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.GetLast().ChildrenDisabled = true;

      // Accept (TraverseAll, or the SkipDescendants fall-through): emit the node's first visit.
      TakeNextVisit();

      return true;
    }

    private async ValueTask<bool> OnVisitingAsync()
    {
      if (await TryPushNextChildAsync().ConfigureAwait(false))
        return true;

      return await BacktrackAsync().ConfigureAwait(false);
    }

    // Unwind finished levels and emit the next owed visit; see the store twin.
    private async ValueTask<bool> BacktrackAsync()
    {
      while (true)
      {
        _Path.RemoveLast();

        if (_Path.Count == 0)
          return await MoveToNextRootNodeAsync().ConfigureAwait(false);

        // A value copy (not a ref local): a ref local may not cross the await below.
        var top = _Path.GetLast();

        if (top.Skipped || top.Position.Depth == _DepthOfLastVisitedNode)
        {
          if (await TryPushNextChildAsync().ConfigureAwait(false))
            return true;

          continue;
        }

        TakeNextVisit();

        return true;
      }
    }

    // THE SEAM, lookahead edition: the active level's next child is the next streamed node iff
    // that node sits exactly one deeper; anything deeper than that is pruned-subtree residue and
    // is discarded unmapped; anything shallower belongs to an ancestor and stays in the
    // lookahead for the backtrack to find.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask<bool> TryPushNextChildAsync()
    {
      if (_Path.GetLast().ChildrenDisabled)
        return false;

      var childDepth = _Path.GetLast().Position.Depth + 1;

      if (!await TryEnsureLookaheadAtOrAboveAsync(childDepth).ConfigureAwait(false))
        return false;

      if (_LookaheadDepth != childDepth)
        return false;

      var value = _LookaheadValue;
      _HasLookahead = false;

      // Re-acquire the ref AFTER the await (a ref local may not cross it) to bump NextSiblingIndex
      // in place. The path is untouched during the lookahead pull, so this is the same level.
      ref var top = ref _Path.GetLast();

      var position = new NodePosition(top.NextSiblingIndex++, childDepth);

      PushLevel(value, position);

      return true;
    }

    // Fill the lookahead and discard past any content deeper than maxDepth (pruned subtrees the
    // stream still holds). False when the stream exhausts first.
    private async ValueTask<bool> TryEnsureLookaheadAtOrAboveAsync(int maxDepth)
    {
      if (_StreamExhausted)
        return false;

      if (!_HasLookahead)
      {
        var read = await _Stream.TryReadNextAsync().ConfigureAwait(false);
        if (!read.HasValue)
        {
          _StreamExhausted = true;
          return false;
        }

        _LookaheadValue = read.Value;
        _LookaheadDepth = read.Depth;
        _HasLookahead = true;
      }

      if (_LookaheadDepth > maxDepth)
      {
        var read = await _Stream.TrySkipToDepthAsync(maxDepth).ConfigureAwait(false);
        if (!read.HasValue)
        {
          _HasLookahead = false;
          _StreamExhausted = true;
          return false;
        }

        _LookaheadValue = read.Value;
        _LookaheadDepth = read.Depth;
      }

      return true;
    }

    // Schedule a node as a new level and publish its scheduling visit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushLevel(TValue value, NodePosition position)
    {
      _Path.AddLast(new Level
      {
        Value = value,
        Position = position,
      });

      Mode = TreenumeratorMode.SchedulingNode;
      Node = value;
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
      Node = top.Value;
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

    public async ValueTask DisposeAsync()
    {
      await _Stream.DisposeAsync().ConfigureAwait(false);
    }
  }
}
