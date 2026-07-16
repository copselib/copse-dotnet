using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Extensions;
using Copse.Linq.Treenumerators; // WhereDepthFirstPath (internal, via InternalsVisibleTo)
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// Depth-first <b>async</b> filter driver and the codegen source of truth for its sync twin:
  /// strip the <c>await</c>s and it becomes the sync driver. Runs the composed VERDICT of a
  /// fused stage chain (docs/OPERATOR_FUSION_DESIGN.md) once per scheduled node, against the
  /// SOURCE context: an accepted verdict's value is published (the path stores projected
  /// values -- opaque cargo; the library never compares nodes) and its strategies apply to the
  /// node's own traversal (PruneAfter's SkipDescendants); a rejected verdict's strategies
  /// drive the inner pull (SkipNode -> promotion of the node's children into its parent's
  /// slot; SkipNodeAndDescendants -> subtree drop). Plain Where/PruneBefore are the
  /// single-stage instantiations. All structural state lives in the shared, color-agnostic
  /// <see cref="WhereDepthFirstPath{TNode}"/> (the SAME struct the sync driver uses, verbatim).
  /// </summary>
  internal sealed class AsyncWhereDepthFirstTreenumerator<TInner, TNode, TVerdictSelector>
    : AsyncTreenumeratorWrapper<TInner, TNode>
    where TVerdictSelector : struct, IVerdictSelector<TInner, TNode>
  {
    public AsyncWhereDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TInner>> innerTreenumeratorFactory,
      TVerdictSelector verdictSelector)
      : base(innerTreenumeratorFactory)
    {
      _VerdictSelector = verdictSelector;

      // Seed the path with a sentinel root: the virtual forest root by definition (its value is
      // never published, so no verdict is owed).
      _Path = new WhereDepthFirstPath<TNode>(default, NodePosition.ForestRoot);
    }

    private readonly TVerdictSelector _VerdictSelector;

    private WhereDepthFirstPath<TNode> _Path;
    private bool _HasCachedChild = false;

    // Accept-side strategies from the last published scheduling visit's verdict, applied on the
    // pull that follows it -- the same protocol moment a consumer's own strategies for that
    // visit arrive, so the two simply union.
    private NodeTraversalStrategies _PendingStageStrategies = NodeTraversalStrategies.TraverseAll;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_HasCachedChild)
      {
        _HasCachedChild = false;
        Publish(ref _Path.AcceptedTop());
        return true;
      }

      nodeTraversalStrategies |= _PendingStageStrategies;
      _PendingStageStrategies = NodeTraversalStrategies.TraverseAll;

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
          if (!OnScheduling(out var rejectedStrategies))
          {
            nodeTraversalStrategies = rejectedStrategies;
            continue;
          }

          return true;
        }

        if (OnVisiting())
          return true;
      }

      return false;
    }

    private bool OnScheduling(out NodeTraversalStrategies rejectedStrategies)
    {
      _Path.PopDeeperThanForScheduling(InnerTreenumerator.Position.Depth);

      // ONE evaluation of the composed stage chain, against the SOURCE context; every user
      // lambda inside sees exactly what the unfused pipeline would have shown it.
      var verdict = _VerdictSelector.GetVerdict(InnerTreenumerator.ToNodeContext());

      if (verdict.Rejected)
      {
        rejectedStrategies = verdict.Strategies;
        return false;
      }

      rejectedStrategies = NodeTraversalStrategies.TraverseAll;
      _PendingStageStrategies = verdict.Strategies;

      // ShouldCacheChild reads the accepted top as the PARENT, so it must run BEFORE the push.
      var cacheChild = _Path.ShouldCacheChild();

      _Path.PushAcceptedChild(verdict.Value, InnerTreenumerator.Position);

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
