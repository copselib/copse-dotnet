using Copse.Core;
using System.Runtime.CompilerServices;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Breadth-first treenumerator over a level-order store: native playback for the flat family,
  /// the structural dual of <see cref="PreorderStoreDepthFirstTreenumerator{TValue, TStore}"/>.
  /// Emits the exact engine visit-stream contract -- same modes, visit counts, raw positions, and
  /// <see cref="NodeTraversalStrategies"/> semantics -- but resolves children by span arithmetic
  /// instead of child enumerators: child ordinal k of a node is store index
  /// firstChildIndex + k, and root ordinal k is store index k (roots are the depth-0 prefix).
  ///
  /// <para>Natural traversal reads the store strictly sequentially -- the visit queue's contents
  /// are consecutive store indices, and children become available in exactly the order the
  /// growing store appends them -- so playback recreates no structure and touches no
  /// delegates.</para>
  ///
  /// <para>The driver logic deliberately mirrors the engine's
  /// (<see cref="BreadthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/>: the schedule
  /// stack of the node under classification plus its SkipNode'd ancestors, the visit queue of
  /// accepted nodes whose front is the active parent, and the slot-carry bit that owes the front
  /// a visit exactly when a child slot enqueued at least one accepted node), because the
  /// contract subtleties live in that structure. Frames here carry a child *cursor* (next child
  /// ordinal) instead of a child enumerator.</para>
  /// </summary>
  public sealed class LevelOrderStoreBreadthFirstTreenumerator<TValue, TStore>
    : TreenumeratorBase<TValue>
    where TStore : ILevelOrderStore<TValue>
  {
    public LevelOrderStoreBreadthFirstTreenumerator(TStore store)
    {
      _Store = store;
      _VisitQueue = new RefSemiDeque<Frame>();
      _ScheduleStack = new RefSemiDeque<Frame>();
    }

    // Non-readonly so interface calls on a struct store mutate it in place rather than a
    // defensive copy.
    private TStore _Store;

    // Accepted nodes, scheduled but not yet fully visited. The front is the active parent.
    private readonly RefSemiDeque<Frame> _VisitQueue;
    // The node being classified, plus any SkipNode'd ancestors whose children are being promoted.
    private readonly RefSemiDeque<Frame> _ScheduleStack;

    private int _RootsSeen;
    private bool _RootsFinished;
    private bool _RootsScheduled;
    // True when the front parent's in-progress child slot has enqueued at least one accepted node.
    private bool _SlotCarry;

    private struct Frame
    {
      public int NodeIndex;
      public NodePosition Position;
      public int VisitCount;
      public int NextChildOrdinal;
      public bool ChildrenDisabled; // SkipDescendants/SkipSiblings: yield no more children.
    }

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // A strategy only applies to the node just scheduled (an empty stack means we have not
      // scheduled anything yet -- the very first move). Visiting nodes ignore the strategy.
      if (Mode == TreenumeratorMode.SchedulingNode && _ScheduleStack.Count > 0)
        ApplyStrategy(nodeTraversalStrategies);

      return Advance();
    }

    // Produce the next single visit (or false when the traversal is exhausted).
    private bool Advance()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the node on top of the schedule stack.
        //    SkipNode'd nodes stay here while their children are promoted; no parent visit is
        //    emitted.
        if (_ScheduleStack.Count > 0)
        {
          if (TryScheduleNextChildOf(ref _ScheduleStack.GetLast()))
            return true;

          _ScheduleStack.RemoveLast();
          continue;
        }

        // 2) Schedule the next root (the forest's children -- no surrounding visits).
        if (!_RootsScheduled)
        {
          if (TryScheduleNextRoot())
            return true;

          _RootsScheduled = true;
          // Enqueues made while scheduling roots have no owing parent; clear the carry.
          _SlotCarry = false;
          continue;
        }

        if (_VisitQueue.Count == 0)
          return false;

        // 3) Visit the active parent and drive its children. Bind the front once so the whole
        //    phase touches the queue a single time.
        ref var front = ref _VisitQueue.GetFirst();

        if (front.VisitCount == 0)
        {
          // Initial visit, before any child is scheduled.
          front.VisitCount = 1;
          PublishVisit(ref front);
          return true;
        }

        if (_SlotCarry)
        {
          // The child slot that just finished enqueued at least one node, so the parent is
          // visited.
          _SlotCarry = false;
          front.VisitCount++;
          PublishVisit(ref front);
          return true;
        }

        if (TryScheduleNextChildOf(ref front))
          return true;

        // The parent has no more children: retire it. The next turn visits the new front.
        _VisitQueue.RemoveFirst();
      }
    }

    // Classify the node just scheduled (the schedule-stack top) by the consumer's strategy.
    private void ApplyStrategy(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (SkipRemainingSiblings())
          _RootsFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an
      // all-bits test), so it must be checked first -- otherwise it would route into the SkipNode
      // promotion path and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
      {
        // Erase the node and its subtree; the slot enqueues nothing.
        _ScheduleStack.RemoveLast();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        // Keep the node resident so Advance can promote its children into its slot.
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        // Accept the node but give it no children, then fall through to the accept below.
        _ScheduleStack.GetLast().ChildrenDisabled = true;

      // Accept (TraverseAll, or the SkipDescendants fall-through): move the node onto the visit
      // queue, and record that this child slot enqueued an accepted node (so the front parent is
      // owed a return visit).
      _VisitQueue.AddLast(_ScheduleStack.RemoveLast());
      _SlotCarry = true;
    }

    // THE SEAM, span-arithmetic edition: the given parent's next child is child ordinal
    // NextChildOrdinal, at store index firstChildIndex + ordinal once the store has grown far
    // enough to prove it exists.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryScheduleNextChildOf(ref Frame parent)
    {
      if (parent.ChildrenDisabled)
        return false;

      var ordinal = parent.NextChildOrdinal;

      if (!_Store.EnsureChildAvailable(parent.NodeIndex, ordinal))
        return false;

      var childIndex = _Store.GetFirstChildIndex(parent.NodeIndex) + ordinal;

      parent.NextChildOrdinal++;

      PushScheduled(childIndex, new NodePosition(ordinal, parent.Position.Depth + 1));

      return true;
    }

    private bool TryScheduleNextRoot()
    {
      if (_RootsFinished || !_Store.EnsureRootAvailable(_RootsSeen))
        return false;

      // Roots are the depth-0 prefix: ordinal and store index coincide.
      PushScheduled(_RootsSeen, new NodePosition(_RootsSeen, 0));

      _RootsSeen++;

      return true;
    }

    // Schedule a node onto the schedule stack and publish its scheduling visit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushScheduled(int nodeIndex, NodePosition position)
    {
      _ScheduleStack.AddLast(new Frame
      {
        NodeIndex = nodeIndex,
        Position = position,
      });

      Mode = TreenumeratorMode.SchedulingNode;
      Node = _Store.GetValue(nodeIndex);
      VisitCount = 0;
      Position = position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishVisit(ref Frame frame)
    {
      Mode = TreenumeratorMode.VisitingNode;
      Node = _Store.GetValue(frame.NodeIndex);
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    // SkipSiblings: silence every frame that could still yield an effective sibling of the
    // just-scheduled node -- its skipped ancestors (the rest of the schedule stack), plus its
    // nearest accepted ancestor (the queue front). Returns true if the node was an effective
    // root, so the driver ends root enumeration.
    private bool SkipRemainingSiblings()
    {
      // Every schedule-stack frame except the node's own (the top) belongs to a skipped ancestor.
      for (int i = 1; i < _ScheduleStack.Count; i++)
        _ScheduleStack.GetFromBack(i).ChildrenDisabled = true;

      // The schedule stack holds only the node and its skipped ancestors, so Count - 1 is the
      // node's skipped-ancestor count. When that equals its depth every ancestor is skipped: the
      // node is an effective root, its siblings are the remaining roots. Otherwise its nearest
      // accepted ancestor is the queue front, whose remaining children we silence.
      if (_ScheduleStack.GetLast().Position.Depth == _ScheduleStack.Count - 1)
        return true;

      _VisitQueue.GetFirst().ChildrenDisabled = true;

      return false;
    }
  }
}
