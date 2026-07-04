using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Depth-first treenumerator over a level-order store: the CROSS-ORDER member of the flat
  /// family's DFT pair (its native twin is
  /// <see cref="PreorderStoreDepthFirstTreenumerator{TValue, TStore}"/>). Same driver, different
  /// child arithmetic: children come from the store's contiguous child spans (child ordinal k of
  /// a node is store index firstChildIndex + k; roots are the depth-0 prefix), so descending is
  /// index chasing rather than a sequential read -- correct and O(depth), but without the native
  /// pair's locality. This is the traversal a completed level-order capture uses to serve the
  /// other dimension (the memo's four-case rule, case 2).
  ///
  /// <para>Emits the exact engine visit-stream contract; see the native twin for the driver
  /// structure (OnScheduling/OnVisiting/Backtrack, skipped levels kept resident for child
  /// promotion, the depth-of-last-visited-node classify).</para>
  /// </summary>
  public sealed class LevelOrderStoreDepthFirstTreenumerator<TValue, TStore>
    : TreenumeratorBase<TValue>
    where TStore : ILevelOrderStore<TValue>
  {
    public LevelOrderStoreDepthFirstTreenumerator(TStore store)
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
      if (_RootsFinished || !_Store.EnsureRootAvailable(_RootsSeen))
        return false;

      // Roots are the depth-0 prefix: ordinal and store index coincide.
      PushLevel(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

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

    // Unwind finished levels and emit the next owed visit; see the native twin.
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

    // THE SEAM, child-span edition: the active level's next child is child ordinal
    // NextChildOrdinal, at store index firstChildIndex + ordinal once the store has grown far
    // enough to prove it exists.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryPushNextChild()
    {
      ref var top = ref _Path.GetLast();

      if (top.ChildrenDisabled)
        return false;

      var ordinal = top.NextChildOrdinal;

      if (!_Store.EnsureChildAvailable(top.NodeIndex, ordinal))
        return false;

      var childIndex = _Store.GetFirstChildIndex(top.NodeIndex) + ordinal;

      top.NextChildOrdinal++;

      var position = new NodePosition(ordinal, top.Position.Depth + 1);

      PushLevel(childIndex, position);

      return true;
    }

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
