using Copse.Core;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Breadth-first treenumerator. Emits the SAME (Mode, Node, VisitCount, Position) visit multiset as
  /// the depth-first treenumerator -- the load-bearing invariant -- but in level order. Every node is
  /// scheduled exactly once; an accepted node is visited once up front plus once after every child slot
  /// that enqueues at least one accepted node.
  ///
  /// <para>All of the engine's state lives in <see cref="BreadthFirstPath{TNode, TChildEnumerator}"/>;
  /// this class is a thin driver over it. Each <see cref="OnMoveNext"/> applies the consumer's strategy
  /// to the node just scheduled (if any), then emits exactly one visit via <see cref="Advance"/>:
  /// (1) while a node sits on the schedule stack, schedule its next child; (2) while roots remain,
  /// schedule the next root; (3) otherwise visit the queue front and drive its children. The only
  /// operations that touch the source are advancing a child enumerator
  /// (<see cref="TryScheduleNextChildOf"/>) or the roots enumerator -- the seams a future async
  /// treenumerator would swap for awaited pulls.</para>
  /// </summary>
  public sealed class BreadthFirstTreenumerator<TValue, TNode, TChildEnumerator>
    : TreenumeratorBase<TValue>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public BreadthFirstTreenumerator(
      IEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetEnumerator();
      _Path = new BreadthFirstPath<TNode, TChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IEnumerator<TNode> _RootsEnumerator;
    // Non-readonly so calls bind `ref this` and the struct's state mutations persist (a readonly field
    // would force a defensive copy and silently lose them).
    private BreadthFirstPath<TNode, TChildEnumerator> _Path;
    private readonly Func<TNode, TValue> _Map;

    private bool _RootsEnumeratorFinished = false;
    private bool _RootsScheduled = false;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // A strategy only applies to the node just scheduled (an empty stack means we have not scheduled
      // anything yet -- the very first move). Visiting nodes ignore the strategy.
      if (Mode == TreenumeratorMode.SchedulingNode && _Path.HasScheduledNode)
        ApplyStrategy(nodeTraversalStrategies);

      return Advance();
    }

    // Produce the next single visit (or false when the traversal is exhausted).
    private bool Advance()
    {
      while (true)
      {
        // 1) Descend: schedule the next child of the node on top of the schedule stack. SkipNode'd
        //    nodes stay here while their children are promoted; no parent visit is emitted.
        if (_Path.HasScheduledNode)
        {
          if (TryScheduleNextChildOf(ref _Path.ScheduleTop))
            return true;

          _Path.PopScheduleStack();
          continue;
        }

        // 2) Schedule the next root (the forest's children -- no surrounding visits).
        if (!_RootsScheduled)
        {
          if (TryScheduleNextRoot())
            return true;

          _RootsScheduled = true;
          // Enqueues made while scheduling roots have no owing parent; clear the carry.
          _Path.ClearSlotCarry();
          continue;
        }

        if (_Path.QueueIsEmpty)
          return false;

        // 3) Visit the active parent and drive its children. Bind the front once so the whole phase
        //    touches the queue a single time.
        ref var front = ref _Path.Front;

        if (front.VisitCount == 0)
        {
          // Initial visit, before any child is scheduled.
          front.VisitCount = 1;
          Publish(ref front);
          return true;
        }

        if (_Path.FrontSlotEnqueuedNode)
        {
          // The child slot that just finished enqueued at least one node, so the parent is visited.
          _Path.ClearSlotCarry();
          front.VisitCount++;
          Publish(ref front);
          return true;
        }

        if (TryScheduleNextChildOf(ref front))
          return true;

        // The parent has no more children: retire it. The next turn visits the new front.
        _Path.RetireFront();
      }
    }

    // Classify the node just scheduled (the schedule-stack top) by the consumer's strategy.
    private void ApplyStrategy(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      // SkipNodeAndDescendants is a superset of SkipNode (HasNodeTraversalStrategies is an all-bits
      // test), so it must be checked first -- otherwise it would route into the SkipNode promotion path
      // and wrongly promote the descendants we are meant to prune.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
      {
        // Erase the node and its subtree; the slot enqueues nothing.
        _Path.PopScheduleStack();
        return;
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        // Keep the node resident so Advance can promote its children into its slot.
        return;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        // Accept the node but give it no children, then fall through to the accept below.
        _Path.DisposeScheduleTopEnumerator();

      // Accept (TraverseAll, or the SkipDescendants fall-through): move the node onto the visit queue.
      _Path.AcceptScheduledNode();
    }

    // THE SEAM: advance the given parent's child enumerator (the schedule-stack top or the queue front)
    // and schedule the child it yields onto the schedule stack. A future async treenumerator replaces
    // the synchronous MoveNext here with an awaited MoveNextAsync; nothing else changes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryScheduleNextChildOf(ref BreadthFirstFrame<TNode, TChildEnumerator> parent)
    {
      if (!parent.ChildEnumerator.MoveNext(out var child))
        return false;

      Publish(ref _Path.PushScheduledChild(parent.Position.Depth, child.Node, child.SiblingIndex));

      return true;
    }

    private bool TryScheduleNextRoot()
    {
      if (_RootsEnumeratorFinished || !_RootsEnumerator.MoveNext())
        return false;

      Publish(ref _Path.PushScheduledRoot(_RootsEnumerator.Current));

      return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Publish(ref BreadthFirstFrame<TNode, TChildEnumerator> frame)
    {
      Mode = frame.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(frame.Node);
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }

    #region Dispose

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _RootsEnumerator?.Dispose();
      _Path.Dispose();
    }

    #endregion Dispose
  }
}
