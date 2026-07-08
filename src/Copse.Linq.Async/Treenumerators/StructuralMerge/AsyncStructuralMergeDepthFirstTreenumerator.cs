using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Treenumerators; // MergeNode
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// Depth-first <b>async</b> <c>StructuralMerge</c>: the direct-style async port of
  /// <c>Copse.Linq.Treenumerators.StructuralMergeDepthFirstTreenumerator</c>. Merges two operand
  /// trees by structural position into a <see cref="MergeNode{TLeft, TRight}"/> stream (the engine
  /// behind Union/Intersection/Subtract/SymmetricDifference). The ONLY seams are the (up to four)
  /// awaited operand pulls in HandleMoveNext; all merge coordination is synchronous.
  ///
  /// <para><b>This is the codegen source of truth for the sync StructuralMerge (DFT) twin.</b> Strip
  /// the <c>await</c>s and it becomes the synchronous driver. No ref locals cross an await -- the two
  /// operand pulls are guarded by booleans computed before them.</para>
  /// </summary>
  public sealed class AsyncStructuralMergeDepthFirstTreenumerator<TLeft, TRight>
    : AsyncTreenumeratorBase<MergeNode<TLeft, TRight>>
  {
    public AsyncStructuralMergeDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TLeft>> leftTreenumeratorFactory,
      Func<IAsyncTreenumerator<TRight>> rightTreenumeratorFactory)
    {
      _LeftTreenumerator = leftTreenumeratorFactory();
      _RightTreenumerator = rightTreenumeratorFactory();
    }

    private readonly IAsyncTreenumerator<TLeft> _LeftTreenumerator;
    private readonly IAsyncTreenumerator<TRight> _RightTreenumerator;

    private bool _LeftSiblingSkipPending = false;
    private bool _RightSiblingSkipPending = false;
    private int _SiblingSkipParentDepth = -1;

    private bool _LeftTreenumeratorFinished;
    private bool _RightTreenumeratorFinished;

    private bool _BothTreenumeratorsFinished => _LeftTreenumeratorFinished && _RightTreenumeratorFinished;

    private Stack<NodeVisit<MergeNode<TLeft, TRight>>> _NodeVisits = new Stack<NodeVisit<MergeNode<TLeft, TRight>>>();

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      await HandleMoveNextForLeftAndRightTreenumeratorsAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (_BothTreenumeratorsFinished)
        return false;

      FixUpNodeVisitsStack(nodeTraversalStrategies);

      var nodeVisit = CreateNextNodeVisit();

      _NodeVisits.Push(nodeVisit);

      UpdateStateFromNodeVisit(nodeVisit);

      return true;
    }

    private async ValueTask HandleMoveNextForLeftAndRightTreenumeratorsAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // SkipSiblings must silence the remaining EFFECTIVE siblings of the just-scheduled node under its
      // effective parent. The operand the node LACKS is not pulled below, so if it holds/will-yield an
      // effective sibling we propagate the skip to it explicitly (see the sync twin for the full rationale).
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
      {
        var effectiveParent = EffectiveParent();

        _SiblingSkipParentDepth = effectiveParent.Depth;

        if (!Node.HasLeft
          && !_LeftTreenumeratorFinished
          && HoldsEffectiveSibling(_LeftTreenumerator.Position, effectiveParent))
          _LeftSiblingSkipPending = true;

        if (!Node.HasRight
          && !_RightTreenumeratorFinished
          && HoldsEffectiveSibling(_RightTreenumerator.Position, effectiveParent))
          _RightSiblingSkipPending = true;
      }

      var callMoveNextOnLeftTreenumerator =
        !_LeftTreenumeratorFinished
        && (_NodeVisits.Count == 0
          || (Node.HasLeft && _LeftTreenumerator.Position == Position));

      var callMoveNextOnRightTreenumerator =
        !_RightTreenumeratorFinished
        && (_NodeVisits.Count == 0
          || (Node.HasRight && _RightTreenumerator.Position == Position));

      if (callMoveNextOnLeftTreenumerator
        && !await _LeftTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
      {
        _LeftTreenumeratorFinished = true;
      }

      if (callMoveNextOnRightTreenumerator
        && !await _RightTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
      {
        _RightTreenumeratorFinished = true;
      }

      // Resolve a stashed sibling-skip once the missing-side operand reaches a SCHEDULING node: deeper than
      // the shared effective parent -> apply the skip; at or above it -> discard (it left the subtree).
      if (_LeftSiblingSkipPending
        && !_LeftTreenumeratorFinished
        && _LeftTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
      {
        if (_LeftTreenumerator.Position.Depth > _SiblingSkipParentDepth)
        {
          if (!await _LeftTreenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNodeAndDescendants | NodeTraversalStrategies.SkipSiblings).ConfigureAwait(false))
            _LeftTreenumeratorFinished = true;
        }

        _LeftSiblingSkipPending = false;
      }

      if (_RightSiblingSkipPending
        && !_RightTreenumeratorFinished
        && _RightTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
      {
        if (_RightTreenumerator.Position.Depth > _SiblingSkipParentDepth)
        {
          if (!await _RightTreenumerator.MoveNextAsync(NodeTraversalStrategies.SkipNodeAndDescendants | NodeTraversalStrategies.SkipSiblings).ConfigureAwait(false))
            _RightTreenumeratorFinished = true;
        }

        _RightSiblingSkipPending = false;
      }
    }

    private void FixUpNodeVisitsStack(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Pop the current node if it was skipped.
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
        _NodeVisits.Pop();

      // Keep _NodeVisits a clean root-to-current ANCESTOR path by popping any top no longer on the path
      // (neither operand at it or within its subtree). See the sync twin for the two collapsed cases.
      while (_NodeVisits.Count > 0)
      {
        var topPosition = _NodeVisits.Peek().Position;
        var leftDepth = _LeftTreenumeratorFinished ? -1 : _LeftTreenumerator.Position.Depth;
        var rightDepth = _RightTreenumeratorFinished ? -1 : _RightTreenumerator.Position.Depth;
        var maxTreenumeratorDepth = Math.Max(leftDepth, rightDepth);

        var treenumeratorAtTop =
          (!_LeftTreenumeratorFinished && _LeftTreenumerator.Position == topPosition)
          || (!_RightTreenumeratorFinished && _RightTreenumerator.Position == topPosition);

        if (maxTreenumeratorDepth <= topPosition.Depth && !treenumeratorAtTop)
          _NodeVisits.Pop();
        else
          break;
      }
    }

    private NodeVisit<MergeNode<TLeft, TRight>> CreateNextNodeVisit()
    {
      // DFS ordering invariant: a parent's between-children return visit must be emitted BEFORE the
      // parent's next child is scheduled (see the sync twin for the desync scenario this guards).
      if (HasPendingParentReturnVisit())
        return RevisitLeadNodeVisitInQueue();

      var nextNodeVisitHasLeft =
        !_LeftTreenumeratorFinished
          && (_RightTreenumeratorFinished
            || _LeftTreenumerator.Position.Depth > _RightTreenumerator.Position.Depth
            || (_LeftTreenumerator.Position.Depth == _RightTreenumerator.Position.Depth
              && _LeftTreenumerator.Position.SiblingIndex <= _RightTreenumerator.Position.SiblingIndex));

      var nextNodeVisitHasRight =
        !_RightTreenumeratorFinished
          && (_LeftTreenumeratorFinished
            || _RightTreenumerator.Position.Depth > _LeftTreenumerator.Position.Depth
            || (_RightTreenumerator.Position.Depth == _LeftTreenumerator.Position.Depth
              && _RightTreenumerator.Position.SiblingIndex <= _LeftTreenumerator.Position.SiblingIndex));

      var createSchedulingNodeVisit =
        (nextNodeVisitHasLeft && _LeftTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        || (nextNodeVisitHasRight && _RightTreenumerator.Mode == TreenumeratorMode.SchedulingNode);

      return
        createSchedulingNodeVisit
        ? CreateSchedulingNodeVisit(nextNodeVisitHasLeft, nextNodeVisitHasRight)
        : RevisitLeadNodeVisitInQueue();
    }

    private bool HasPendingParentReturnVisit()
    {
      if (_NodeVisits.Count == 0)
        return false;

      var parent = _NodeVisits.Peek();

      var leftOwes = OwesReturnVisit(_LeftTreenumerator, _LeftTreenumeratorFinished, parent.Node.HasLeft, parent);
      var rightOwes = OwesReturnVisit(_RightTreenumerator, _RightTreenumeratorFinished, parent.Node.HasRight, parent);

      if (leftOwes
        && !StillInCurrentChildSlotOf(_RightTreenumerator, _RightTreenumeratorFinished, parent.Position.Depth))
        return true;

      if (rightOwes
        && !StillInCurrentChildSlotOf(_LeftTreenumerator, _LeftTreenumeratorFinished, parent.Position.Depth))
        return true;

      return false;
    }

    private static bool StillInCurrentChildSlotOf<TInner>(
      IAsyncTreenumerator<TInner> treenumerator,
      bool finished,
      int parentDepth)
    {
      var parentDirectChildDepth = parentDepth + 1;
      return !finished && treenumerator.Position.Depth > parentDirectChildDepth;
    }

    private static bool OwesReturnVisit<TInner>(
      IAsyncTreenumerator<TInner> treenumerator,
      bool finished,
      bool parentHasThisSide,
      NodeVisit<MergeNode<TLeft, TRight>> parent)
      => !finished
        && parentHasThisSide
        && treenumerator.Mode == TreenumeratorMode.VisitingNode
        && treenumerator.Position == parent.Position
        && treenumerator.VisitCount == parent.VisitCount + 1;

    private NodeVisit<MergeNode<TLeft, TRight>> CreateSchedulingNodeVisit(
      bool includeLeft,
      bool includeRight)
    {
      var node =
        new MergeNode<TLeft, TRight>(
          includeLeft ? _LeftTreenumerator.Node : default,
          includeRight ? _RightTreenumerator.Node : default,
          includeLeft,
          includeRight);

      var nodeVisit =
        new NodeVisit<MergeNode<TLeft, TRight>>(
          TreenumeratorMode.SchedulingNode,
          node,
          includeLeft ? _LeftTreenumerator.VisitCount : _RightTreenumerator.VisitCount,
          includeLeft ? _LeftTreenumerator.Position : _RightTreenumerator.Position);

      return nodeVisit;
    }

    private NodeVisit<MergeNode<TLeft, TRight>> RevisitLeadNodeVisitInQueue()
    {
      var nodeVisit = _NodeVisits.Pop();

      nodeVisit =
        new NodeVisit<MergeNode<TLeft, TRight>>(
          TreenumeratorMode.VisitingNode,
          nodeVisit.Node,
          nodeVisit.VisitCount + 1,
          nodeVisit.Position);

      return nodeVisit;
    }

    private void UpdateStateFromNodeVisit(NodeVisit<MergeNode<TLeft, TRight>> nodeVisit)
    {
      Node = nodeVisit.Node;
      VisitCount = nodeVisit.VisitCount;
      Mode = nodeVisit.Mode;
      Position = nodeVisit.Position;
    }

    private NodePosition EffectiveParent()
    {
      if (_NodeVisits.Count < 2)
        return ForestRoot;

      var topVisit = _NodeVisits.Pop();
      var parent = _NodeVisits.Peek().Position;
      _NodeVisits.Push(topVisit);

      return parent.Depth < topVisit.Position.Depth ? parent : ForestRoot;
    }

    private static readonly NodePosition ForestRoot = NodePosition.ForestRoot; // canonical name now lives in Core

    private static bool HoldsEffectiveSibling(NodePosition operandPosition, NodePosition parent)
      => operandPosition.Depth > parent.Depth || operandPosition == parent;

    protected override async ValueTask OnDisposingAsync()
    {
      await base.OnDisposingAsync().ConfigureAwait(false);
      await _LeftTreenumerator.DisposeAsync().ConfigureAwait(false);
      await _RightTreenumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
