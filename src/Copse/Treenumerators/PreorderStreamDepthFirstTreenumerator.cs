using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Depth-first treenumerator over a forward-only preorder stream: the streaming tier of
  /// <see cref="PreorderStoreDepthFirstTreenumerator{TValue, TStore}"/>. Same driver, but the
  /// source affords one pass and no random access, so levels carry their VALUES (there is no
  /// index to look anything up by) and child detection is a one-token lookahead: the next
  /// streamed node is the active level's child iff its depth is exactly one deeper.
  /// O(depth) resident state -- the root-to-current path plus the lookahead slot.
  ///
  /// <para>Skips are LAZY DISCARDS, all through one seam: whenever the lookahead is deeper than
  /// the level a child is being requested for, the gap content belongs to subtrees the consumer
  /// pruned (SkipDescendants' children, a SkipNodeAndDescendants'd subtree, a childless-skip
  /// remainder), and <see cref="IPreorderStream{TValue}.TrySkipToDepth"/> discards it without
  /// materializing values -- I/O only, the value map never runs. Nothing is ever discarded
  /// eagerly, so the stream is read at most once and only as far as the traversal demands.</para>
  ///
  /// <para>The treenumerator OWNS the stream and disposes it -- the reader-ownership hook the
  /// serializer's contract needs (each acquisition opens a fresh reader; disposing the
  /// treenumerator closes it).</para>
  /// </summary>
  public sealed class PreorderStreamDepthFirstTreenumerator<TValue, TStream>
    : TreenumeratorBase<TValue>
    where TStream : IPreorderStream<TValue>
  {
    public PreorderStreamDepthFirstTreenumerator(TStream stream)
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

    private struct Level
    {
      public TValue Value;
      public NodePosition Position;
      public int VisitCount;
      public bool Skipped;           // SkipNode'd: no visits, resident only to promote children.
      public bool ChildrenDisabled;  // SkipDescendants/SkipSiblings: yield no more children.
      public int NextSiblingIndex;
    }

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.Count == 0)
        return MoveToNextRootNode();

      // The strategy applies to the node just scheduled; visiting nodes ignore it.
      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnScheduling(nodeTraversalStrategies);

      return OnVisiting();
    }

    private bool MoveToNextRootNode()
    {
      if (_RootsFinished)
        return false;

      // Roots are the forest's children: the same lookahead logic with the virtual forest
      // level at depth -1 (any remaining deeper content is a pruned ex-root's subtree).
      if (!TryEnsureLookaheadAtOrAbove(0))
        return false;

      var value = _LookaheadValue;
      _HasLookahead = false;

      PushLevel(value, new NodePosition(_RootsSeen++, 0));

      return true;
    }

    private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (SkipRemainingSiblings())
          _RootsFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an
      // all-bits test), so it must be checked first -- otherwise it would route into the SkipNode
      // promotion path and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return Backtrack();

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.GetLast().Skipped = true;

        if (TryPushNextChild())
          return true;

        // No children to promote: a childless SkipNode'd node emits nothing.
        return Backtrack();
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.GetLast().ChildrenDisabled = true;

      // Accept (TraverseAll, or the SkipDescendants fall-through): emit the node's first visit.
      TakeNextVisit();

      return true;
    }

    private bool OnVisiting()
    {
      if (TryPushNextChild())
        return true;

      return Backtrack();
    }

    // Unwind finished levels and emit the next owed visit; see the store twin.
    private bool Backtrack()
    {
      while (true)
      {
        _Path.RemoveLast();

        if (_Path.Count == 0)
          return MoveToNextRootNode();

        ref var top = ref _Path.GetLast();

        if (top.Skipped || top.Position.Depth == _DepthOfLastVisitedNode)
        {
          if (TryPushNextChild())
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
    private bool TryPushNextChild()
    {
      ref var top = ref _Path.GetLast();

      if (top.ChildrenDisabled)
        return false;

      var childDepth = top.Position.Depth + 1;

      if (!TryEnsureLookaheadAtOrAbove(childDepth))
        return false;

      if (_LookaheadDepth != childDepth)
        return false;

      var value = _LookaheadValue;
      _HasLookahead = false;

      var position = new NodePosition(top.NextSiblingIndex++, childDepth);

      PushLevel(value, position);

      return true;
    }

    // Fill the lookahead and discard past any content deeper than maxDepth (pruned subtrees the
    // stream still holds). False when the stream exhausts first.
    private bool TryEnsureLookaheadAtOrAbove(int maxDepth)
    {
      if (_StreamExhausted)
        return false;

      if (!_HasLookahead)
      {
        if (!_Stream.TryReadNext(out _LookaheadValue, out _LookaheadDepth))
        {
          _StreamExhausted = true;
          return false;
        }

        _HasLookahead = true;
      }

      if (_LookaheadDepth > maxDepth)
      {
        if (!_Stream.TrySkipToDepth(maxDepth, out _LookaheadValue, out _LookaheadDepth))
        {
          _HasLookahead = false;
          _StreamExhausted = true;
          return false;
        }
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

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _Stream.Dispose();
    }
  }
}
