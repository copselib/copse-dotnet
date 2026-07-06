using Copse.Core;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Traversal
{
  /// <summary>A scheduled node: its visit state and its child enumerator in one slot, only ever touched by ref.</summary>
  internal struct BreadthFirstFrame<TNode, TEnumerator>
    where TEnumerator : IDisposable
  {
    public BreadthFirstFrame(TNode node, NodePosition position, TEnumerator childEnumerator)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
      ChildEnumerator = childEnumerator;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
    public TEnumerator ChildEnumerator;
  }

  /// <summary>
  /// The two structures of a breadth-first traversal, as a color-agnostic shared struct (the ports of
  /// <c>BreadthFirstPath</c> / <c>BreadthFirstFrame</c>, constrained only on <see cref="IDisposable"/> so
  /// a direct-style driver -- sync or async -- can share it). Sans-I/O: exposes the two active enumerators
  /// (<see cref="ScheduleTop"/>, <see cref="Front"/>) by ref for the driver to advance; every other op is
  /// pure synchronous state.
  /// </summary>
  internal struct BreadthFirstPathState<TNode, TEnumerator> : IDisposable
    where TEnumerator : IDisposable
  {
    public BreadthFirstPathState(Func<NodeContext<TNode>, TEnumerator> childEnumeratorFactory)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _VisitQueue = new RefSemiDeque<BreadthFirstFrame<TNode, TEnumerator>>();
      _ScheduleStack = new RefSemiDeque<BreadthFirstFrame<TNode, TEnumerator>>();
      _RootNodesSeen = 0;
      _CurrentSlotEnqueuedNode = false;
    }

    private readonly Func<NodeContext<TNode>, TEnumerator> _ChildEnumeratorFactory;

    private readonly RefSemiDeque<BreadthFirstFrame<TNode, TEnumerator>> _VisitQueue;
    private readonly RefSemiDeque<BreadthFirstFrame<TNode, TEnumerator>> _ScheduleStack;

    private int _RootNodesSeen;
    private bool _CurrentSlotEnqueuedNode;

    public bool HasScheduledNode => _ScheduleStack.Count > 0;
    public bool QueueIsEmpty => _VisitQueue.Count == 0;
    public bool FrontSlotEnqueuedNode => _CurrentSlotEnqueuedNode;

    public ref BreadthFirstFrame<TNode, TEnumerator> ScheduleTop
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _ScheduleStack.GetLast();
    }

    public ref BreadthFirstFrame<TNode, TEnumerator> Front
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _VisitQueue.GetFirst();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref BreadthFirstFrame<TNode, TEnumerator> PushScheduledChild(int parentDepth, TNode node, int siblingIndex)
      => ref PushScheduled(node, new NodePosition(siblingIndex, parentDepth + 1));

    public ref BreadthFirstFrame<TNode, TEnumerator> PushScheduledRoot(TNode node)
      => ref PushScheduled(node, new NodePosition(_RootNodesSeen++, 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref BreadthFirstFrame<TNode, TEnumerator> PushScheduled(TNode node, NodePosition position)
    {
      _ScheduleStack.AddLast(
        new BreadthFirstFrame<TNode, TEnumerator>(node, position, _ChildEnumeratorFactory(new NodeContext<TNode>(node, position))));
      return ref _ScheduleStack.GetLast();
    }

    public void PopScheduleStack() => _ScheduleStack.RemoveLast().ChildEnumerator.Dispose();

    public void DisposeScheduleTopEnumerator() => _ScheduleStack.GetLast().ChildEnumerator.Dispose();

    public void AcceptScheduledNode()
    {
      _VisitQueue.AddLast(_ScheduleStack.RemoveLast());
      _CurrentSlotEnqueuedNode = true;
    }

    public void ClearSlotCarry() => _CurrentSlotEnqueuedNode = false;

    public void RetireFront() => _VisitQueue.RemoveFirst().ChildEnumerator.Dispose();

    public bool SkipRemainingSiblings()
    {
      for (int i = 1; i < _ScheduleStack.Count; i++)
        _ScheduleStack.GetFromBack(i).ChildEnumerator.Dispose();

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
