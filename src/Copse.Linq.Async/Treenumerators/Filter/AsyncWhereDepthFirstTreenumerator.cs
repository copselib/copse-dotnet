using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using Copse.Linq.Treenumerators; // WhereDepthFirstPath (internal, via InternalsVisibleTo)
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// Depth-first <b>async</b> <c>Where</c> and the codegen source of truth for its sync twin: strip
  /// the <c>await</c>s and it becomes the sync Where driver. Filters the inner visit stream,
  /// promoting a predicate-skipped node's children into its parent's slot. All structural state
  /// lives in the shared, color-agnostic <see cref="WhereDepthFirstPath{TNode}"/> (the SAME struct
  /// the sync driver uses, verbatim) -- the test that the codegen approach holds for the library's
  /// most intricate operator, not just the engine.
  /// </summary>
  public sealed class AsyncWhereDepthFirstTreenumerator<TNode>
    : AsyncTreenumeratorWrapper<TNode>
  {
    public AsyncWhereDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate,
      NodeTraversalStrategies nodeTraversalStrategy)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
      _NodeTraversalStrategy = nodeTraversalStrategy;

      // Seed the path with a sentinel root: the virtual forest root by definition.
      _Path = new WhereDepthFirstPath<TNode>(InnerTreenumerator.Node, NodePosition.ForestRoot);
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;
    private readonly NodeTraversalStrategies _NodeTraversalStrategy;

    private WhereDepthFirstPath<TNode> _Path;
    private bool _HasCachedChild = false;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_HasCachedChild)
      {
        _HasCachedChild = false;
        Publish(ref _Path.AcceptedTop());
        return true;
      }

      // If the consumer skipped the node we just scheduled, move it to the skipped stack so its
      // descendants get promoted. Never move the sentinel (the only node when AcceptedCount == 1).
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

      // Enumerate until we yield something or exhaust the inner enumerator. THE SEAM: awaited inner pull.
      while (await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
      {
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
      _Path.PopDeeperThanForScheduling(InnerTreenumerator.Position.Depth);

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
