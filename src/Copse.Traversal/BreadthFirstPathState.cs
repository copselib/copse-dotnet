using Copse.Core;
using System;
using System.Runtime.CompilerServices;

namespace Copse.Traversal
{
  /// <summary>
  /// The two structures of a breadth-first traversal, as a color-agnostic shared struct (the ports of
  /// <c>BreadthFirstPath</c> / <c>BreadthFirstFrame</c>, constrained only on <see cref="IDisposable"/> so
  /// a direct-style driver -- sync or async -- can share it). Sans-I/O: exposes the two active enumerators
  /// (<see cref="ScheduleTop"/>, <see cref="Front"/>) by ref for the driver to advance; every other op is
  /// pure synchronous state.
  /// </summary>
  internal struct BreadthFirstPathState<THandle, TEnumerator> : IDisposable
    where TEnumerator : IDisposable
  {
    public BreadthFirstPathState(Func<NodeContext<THandle>, TEnumerator> childEnumeratorFactory)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _VisitQueue = new RefSemiDeque<BreadthFirstFrame<THandle, TEnumerator>>();
      _ScheduleStack = new RefSemiDeque<BreadthFirstFrame<THandle, TEnumerator>>();
      _RootNodesSeen = 0;
      _CurrentSlotEnqueuedNode = false;
    }

    private readonly Func<NodeContext<THandle>, TEnumerator> _ChildEnumeratorFactory;

    private readonly RefSemiDeque<BreadthFirstFrame<THandle, TEnumerator>> _VisitQueue;
    private readonly RefSemiDeque<BreadthFirstFrame<THandle, TEnumerator>> _ScheduleStack;

    private int _RootNodesSeen;
    private bool _CurrentSlotEnqueuedNode;

    public bool HasScheduledNode => _ScheduleStack.Count > 0;
    public bool QueueIsEmpty => _VisitQueue.Count == 0;
    public bool FrontSlotEnqueuedNode => _CurrentSlotEnqueuedNode;

    public ref BreadthFirstFrame<THandle, TEnumerator> ScheduleTop
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _ScheduleStack.GetLast();
    }

    public ref BreadthFirstFrame<THandle, TEnumerator> Front
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => ref _VisitQueue.GetFirst();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref BreadthFirstFrame<THandle, TEnumerator> PushScheduledChild(int parentDepth, THandle node, int siblingIndex)
      => ref PushScheduled(node, new NodePosition(siblingIndex, parentDepth + 1));

    public ref BreadthFirstFrame<THandle, TEnumerator> PushScheduledRoot(THandle node)
      => ref PushScheduled(node, new NodePosition(_RootNodesSeen++, 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref BreadthFirstFrame<THandle, TEnumerator> PushScheduled(THandle node, NodePosition position)
    {
      _ScheduleStack.AddLast(
        new BreadthFirstFrame<THandle, TEnumerator>(node, position, _ChildEnumeratorFactory(new NodeContext<THandle>(node, position))));
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
