using Copse.Core;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Treenumerators
{
  // A scheduled node: its visit state and its (mutable-struct) child enumerator in one slot, only ever
  // touched by ref so the enumerator is never copied. (Cohesive here -- unlike the depth-first engine,
  // the breadth-first engine keeps full node state for every resident node anyway, so bundling the
  // enumerator in costs no extra memory.)
  internal struct BreadthFirstFrame<TNode, TChildEnumerator>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public BreadthFirstFrame(TNode node, NodePosition position, TChildEnumerator childEnumerator)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
      ChildEnumerator = childEnumerator;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
    public TChildEnumerator ChildEnumerator;
  }

  /// <summary>
  /// The two structures of a breadth-first traversal and all of their bookkeeping, isolated behind
  /// intention-revealing operations so the treenumerator that drives them stays a thin shell.
  ///
  /// <para><b>Sans-I/O.</b> The path NEVER pulls a child itself. It exposes the two active enumerators
  /// -- the schedule-stack top (<see cref="ScheduleTop"/>) and the visit-queue front
  /// (<see cref="Front"/>) -- by <c>ref</c> for the driver to advance. Every other operation is pure
  /// synchronous state, so a sync and an async treenumerator can share this class and differ only at
  /// the lines that advance an enumerator.</para>
  ///
  /// <list type="bullet">
  /// <item><b>_VisitQueue</b> (FIFO): accepted nodes scheduled but not yet fully visited; the front is
  /// the active parent.</item>
  /// <item><b>_ScheduleStack</b> (LIFO): the node being classified, plus any SkipNode'd ancestors kept
  /// resident so their children promote into the skipped node's slot.</item>
  /// </list>
  ///
  /// <para>Whether the front parent is owed a return visit is a single bit
  /// (<see cref="FrontSlotEnqueuedNode"/>): a child slot earns the parent a visit exactly when it
  /// enqueues at least one accepted node.</para>
  ///
  /// <para><b>Layout.</b> A mutable struct, embedded as the driver's single <c>_Path</c> field so there
  /// is no extra object indirection on the hot path. It is never copied -- only ever accessed as
  /// <c>_Path.Member</c> (so calls bind <c>ref this</c> and its mutations persist) -- and every
  /// <c>ref</c> it returns points into the heap-allocated deques, never into the struct itself.</para>
  /// </summary>
  internal struct BreadthFirstPath<TNode, TChildEnumerator> : IDisposable
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public BreadthFirstPath(Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _VisitQueue = new RefSemiDeque<BreadthFirstFrame<TNode, TChildEnumerator>>();
      _ScheduleStack = new RefSemiDeque<BreadthFirstFrame<TNode, TChildEnumerator>>();
      _RootNodesSeen = 0;
      _CurrentSlotEnqueuedNode = false;
    }

    private readonly Func<NodeContext<TNode>, TChildEnumerator> _ChildEnumeratorFactory;

    // Accepted nodes, scheduled but not yet fully visited. The front is the active parent.
    private readonly RefSemiDeque<BreadthFirstFrame<TNode, TChildEnumerator>> _VisitQueue;
    // The node being classified, plus any SkipNode'd ancestors whose children are being promoted.
    private readonly RefSemiDeque<BreadthFirstFrame<TNode, TChildEnumerator>> _ScheduleStack;

    private int _RootNodesSeen;
    // True when the front parent's in-progress child slot has enqueued at least one accepted node.
    private bool _CurrentSlotEnqueuedNode;

    public bool HasScheduledNode => _ScheduleStack.Count > 0;

    public bool QueueIsEmpty => _VisitQueue.Count == 0;

    public bool FrontSlotEnqueuedNode => _CurrentSlotEnqueuedNode;

    // The active enumerators the driver advances. THE I/O SEAMS.
    public ref BreadthFirstFrame<TNode, TChildEnumerator> ScheduleTop
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _ScheduleStack.GetLast();
    }

    public ref BreadthFirstFrame<TNode, TChildEnumerator> Front
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _VisitQueue.GetFirst();
    }

    // Schedule a freshly pulled child (of a parent at parentDepth) as a new schedule-stack node;
    // returns its frame so the driver can publish it without a second lookup.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref BreadthFirstFrame<TNode, TChildEnumerator> PushScheduledChild(int parentDepth, TNode node, int siblingIndex)
      => ref PushScheduled(node, new NodePosition(siblingIndex, parentDepth + 1));

    // Schedule the next root as a new schedule-stack node; returns its frame for publishing.
    public ref BreadthFirstFrame<TNode, TChildEnumerator> PushScheduledRoot(TNode node)
      => ref PushScheduled(node, new NodePosition(_RootNodesSeen++, 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref BreadthFirstFrame<TNode, TChildEnumerator> PushScheduled(TNode node, NodePosition position)
    {
      _ScheduleStack.AddLast(
        new BreadthFirstFrame<TNode, TChildEnumerator>(node, position, _ChildEnumeratorFactory(new NodeContext<TNode>(node, position))));
      return ref _ScheduleStack.GetLast();
    }

    // SkipNodeAndDescendants, or a schedule-stack node whose children are exhausted: drop the top.
    public void PopScheduleStack() => _ScheduleStack.RemoveLast().ChildEnumerator.Dispose();

    // SkipDescendants: prune the just-scheduled node's children by disposing its unused enumerator.
    public void DisposeScheduleTopEnumerator() => _ScheduleStack.GetLast().ChildEnumerator.Dispose();

    // Accept the just-scheduled node: move its whole frame onto the back of the visit queue, and record
    // that this child slot enqueued an accepted node (so the front parent is owed a return visit).
    public void AcceptScheduledNode()
    {
      _VisitQueue.AddLast(_ScheduleStack.RemoveLast());
      _CurrentSlotEnqueuedNode = true;
    }

    // Enqueues made while scheduling roots have no owing parent (and a front parent's owed return visit
    // is consumed): clear the slot carry.
    public void ClearSlotCarry() => _CurrentSlotEnqueuedNode = false;

    // The front parent has no more children: retire it.
    public void RetireFront() => _VisitQueue.RemoveFirst().ChildEnumerator.Dispose();

    // SkipSiblings: silence the remaining siblings of the just-scheduled node by disposing every
    // enumerator that would still yield them (its skipped ancestors, plus its nearest accepted ancestor
    // -- the queue front). Returns true if the node was an effective root, so the driver ends roots.
    public bool SkipRemainingSiblings()
    {
      // Every schedule-stack enumerator except the node's own (the top) belongs to a skipped ancestor.
      for (int i = 1; i < _ScheduleStack.Count; i++)
        _ScheduleStack.GetFromBack(i).ChildEnumerator.Dispose();

      // _ScheduleStack holds only the node and its skipped ancestors, so _ScheduleStack.Count - 1 is the
      // node's skipped-ancestor count. When that equals its depth every ancestor is skipped: the node is
      // an effective root, its siblings are the remaining roots. Otherwise its nearest accepted ancestor
      // is the queue front, whose remaining children we silence.
      if (_ScheduleStack.GetLast().Position.Depth == _ScheduleStack.Count - 1)
        return true;

      _VisitQueue.GetFirst().ChildEnumerator.Dispose();
      return false;
    }

    public void Dispose()
    {
      if (_VisitQueue != null)
        while (_VisitQueue.Count > 0)
          _VisitQueue.RemoveLast().ChildEnumerator.Dispose();

      if (_ScheduleStack != null)
        while (_ScheduleStack.Count > 0)
          _ScheduleStack.RemoveLast().ChildEnumerator.Dispose();
    }
  }
}
