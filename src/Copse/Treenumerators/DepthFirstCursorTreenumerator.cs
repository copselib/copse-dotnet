using Copse.Core;
using Copse.Traversal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TraversalBacktrackStep = Copse.Traversal.DepthFirstBacktrackStep;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Depth-first sync driver identical to <see cref="DepthFirstDirectTreenumerator{TValue, TNode, TChildEnumerator}"/>
  /// EXCEPT the child pull is a by-value <see cref="IChildCursor{TNode}"/> (<c>ChildResult MoveNext()</c>)
  /// instead of an <c>out</c>-param <c>IChildEnumerator</c>. Same shared path, same control flow --
  /// so an A/B of Direct vs Cursor isolates the cost of return-by-value vs <c>out</c> on the hot pull.
  /// </summary>
  public sealed class DepthFirstCursorTreenumerator<TValue, TNode, TCursor>
    : TreenumeratorBase<TValue>
    where TCursor : IChildCursor<TNode>
  {
    public DepthFirstCursorTreenumerator(
      IEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TCursor> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetEnumerator();
      _Path = new DepthFirstPathState<TNode, TCursor>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IEnumerator<TNode> _RootsEnumerator;
    private DepthFirstPathState<TNode, TCursor> _Path;
    private readonly Func<TNode, TValue> _Map;

    private bool _RootsEnumeratorFinished = false;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Path.IsEmpty)
        return MoveToNextRootNode();

      if (Mode == TreenumeratorMode.SchedulingNode)
        return OnScheduling(nodeTraversalStrategies);

      return OnVisiting();
    }

    private bool MoveToNextRootNode()
    {
      if (_RootsEnumeratorFinished || !_RootsEnumerator.MoveNext())
        return false;

      Publish(ref _Path.PushRoot(_RootsEnumerator.Current));
      return true;
    }

    private bool OnScheduling(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipSiblings))
        if (_Path.SkipRemainingSiblings())
          _RootsEnumeratorFinished = true;

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNodeAndDescendants))
        return Backtrack();

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.SkipCurrentNode();

        if (TryPushNextChild())
          return true;

        return Backtrack();
      }

      if (nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipDescendants))
        _Path.DisposeCurrentEnumerator();

      Publish(ref _Path.TakeNextVisit());
      return true;
    }

    private bool OnVisiting()
    {
      if (TryPushNextChild())
        return true;

      return Backtrack();
    }

    private bool Backtrack()
    {
      while (true)
      {
        switch (_Path.PopFinishedLevelAndClassify())
        {
          case TraversalBacktrackStep.GoToRoot:
            return MoveToNextRootNode();

          case TraversalBacktrackStep.PromoteNextChild:
            if (TryPushNextChild())
              return true;
            continue;

          default: // EmitReturnVisit
            Publish(ref _Path.TakeNextVisit());
            return true;
        }
      }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryPushNextChild()
    {
      var result = _Path.TopEnumerator.MoveNext();
      if (!result.HasChild)
        return false;

      Publish(ref _Path.PushChild(result.Child.Node, result.Child.SiblingIndex));
      return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Publish(ref Copse.Traversal.DepthFirstNodeState<TNode> node)
    {
      Mode = node.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(node.Node);
      VisitCount = node.VisitCount;
      Position = node.Position;
    }

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _RootsEnumerator?.Dispose();
      _Path.Dispose();
    }
  }
}
