using Copse.Core;
using Copse.Linq.Extensions;
using System;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// Depth-first <c>Where</c>: filters the inner visit stream, promoting a predicate-skipped node's
  /// children into its parent's slot (compressing depth and renumbering siblings) without ever comparing
  /// node values.
  ///
  /// <para>All structural state lives in <see cref="WhereDepthFirstPath{TNode}"/>; this class is a thin
  /// driver over it. The only operations that touch the source are the two I/O seams pulling the next
  /// inner visit (<see cref="InnerTreenumerator"/>.MoveNext) and evaluating <see cref="_Predicate"/>;
  /// every other line is shared synchronous state on the path. The driver reads the inner
  /// Mode/Node/Position once per step and passes them into the path operations.</para>
  /// </summary>
  internal class WhereDepthFirstTreenumerator<TNode>
    : TreenumeratorWrapper<TNode>
  {
    public WhereDepthFirstTreenumerator(
      Func<ITreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate,
      NodeTraversalStrategies nodeTraversalStrategy)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
      _NodeTraversalStrategy = nodeTraversalStrategy;

      // Seed the path with a sentinel root taken from the inner treenumerator's initial position.
      // The sentinel is the virtual forest root BY DEFINITION; do not derive its position from the
      // inner's pre-enumeration state (that state is contractual, but a definition beats a reading).
      _Path = new WhereDepthFirstPath<TNode>(InnerTreenumerator.Node, NodePosition.ForestRoot);
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;
    private readonly NodeTraversalStrategies _NodeTraversalStrategy;

    // Non-readonly so calls bind `ref this` and the struct's state mutations persist (a readonly field
    // would force a defensive copy and silently lose them -- see DepthFirstTreenumerator.cs:37-39).
    private WhereDepthFirstPath<TNode> _Path;

    private bool _HasCachedChild = false;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_HasCachedChild)
      {
        _HasCachedChild = false;

        Publish(ref _Path.AcceptedTop());

        return true;
      }

      // If the consumer skipped the node we just scheduled, move it to the skipped stack so its
      // descendants get promoted. We must never move the sentinel (the only node present when
      // AcceptedCount == 1): it is a virtual parent, not a node we ever yielded.
      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        && _Path.AcceptedCount > 1
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _Path.MoveLastAcceptedToSkipped();
      }

      if (Mode == TreenumeratorMode.VisitingNode)
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

      // Do not apply any traversal strategies to the sentinel node.
      if (InnerTreenumerator.Position.IsForestRoot)
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

      // Enumerate until we yield something or exhaust the inner enumerator.
      while (InnerTreenumerator.MoveNext(nodeTraversalStrategies))
      {
        // Reset the traversal strategy.
        nodeTraversalStrategies = NodeTraversalStrategies.TraverseAll;

        if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
        {
          if (!OnScheduling())
          {
            nodeTraversalStrategies = _NodeTraversalStrategy;
            continue;
          }

          return true;
        }

        if (OnVisiting())
          return true;
      }

      return false;
    }

    private bool OnScheduling()
    {
      // Unwind finished levels and top up the level we returned to so the sibling index is correct.
      _Path.PopDeeperThanForScheduling(InnerTreenumerator.Position.Depth);

      // Check if the current node visit should be skipped.
      if (!_Predicate(InnerTreenumerator.ToNodeContext()))
        return false;

      // ShouldCacheChild reads the accepted top as the PARENT, so it must run BEFORE the push.
      var cacheChild = _Path.ShouldCacheChild();

      _Path.PushAcceptedChild(InnerTreenumerator.Node, InnerTreenumerator.Position);

      if (cacheChild)
      {
        _HasCachedChild = true;
        Publish(ref _Path.TakeParentReturnVisit());
      }
      else
      {
        Publish(ref _Path.AcceptedTop());
      }

      return true;
    }

    private bool OnVisiting()
    {
      _Path.PopDeeperThanForVisiting(
        InnerTreenumerator.Position.Depth,
        out var removedVisitedNodes,
        out var removedSkippedNodes);

      if (_Path.ShouldSuppressVisit(InnerTreenumerator.Position, removedVisitedNodes, removedSkippedNodes))
        return false;

      Publish(ref _Path.TakeCurrentVisit());

      return true;
    }

    private void Publish(ref WhereDepthFirstPath<TNode>.InternalNodeVisit frame)
    {
      Mode = frame.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;

      _Path.RecordPublished(frame.OriginalPosition.Depth, Mode == TreenumeratorMode.VisitingNode);

      Node = frame.Node;
      VisitCount = frame.VisitCount;
      Position = frame.Position;
    }
  }
}
