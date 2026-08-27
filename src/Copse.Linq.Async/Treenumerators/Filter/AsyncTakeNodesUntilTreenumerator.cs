using Copse;
using Copse.Core;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// <b>async</b> <c>TakeNodesUntil</c> and the codegen source of truth for its sync twin: strip the
  /// <c>await</c>s and it becomes the synchronous driver. Forwards the inner visit stream until a
  /// scheduled node matches the predicate; from there scheduling stops (the matched node's subtree
  /// and later siblings are pruned), optionally keeping the matched node itself
  /// (<c>keepFinalNode</c>). Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncTakeNodesUntilTreenumerator<TNode>
    : AsyncTreenumeratorWrapper<TNode>
  {
    public AsyncTakeNodesUntilTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeAndPosition<TNode>, bool> predicate,
      bool keepFinalNode)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
      _KeepFinalNode = keepFinalNode;
    }

    private readonly Func<NodeAndPosition<TNode>, bool> _Predicate;
    private bool _KeepFinalNode;
    private bool _StopSchedulingNodes;
    private bool _FinalVisitRemaining = false;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (EnumerationFinished)
        return false;

      if (_StopSchedulingNodes)
        return await OnSchedulingStoppedAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      if (InnerTreenumerator.Mode == TreenumeratorMode.VisitingNode)
      {
        UpdateState();
        return true;
      }

      if (_Predicate(InnerTreenumerator.ToNodeAndPosition()))
      {
        _StopSchedulingNodes = true;

        if (_KeepFinalNode)
          _FinalVisitRemaining = true;
        else
          return await OnSchedulingStoppedAsync(nodeTraversalStrategies).ConfigureAwait(false);
      }

      UpdateState();

      return true;
    }

    private async ValueTask<bool> OnSchedulingStoppedAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_FinalVisitRemaining == true)
      {
        _FinalVisitRemaining = false;
        nodeTraversalStrategies |= NodeTraversalStrategies.PruneDescendantsAndSiblings;
      }
      else
      {
        // Use PruneSubtree instead of PruneSubtreeAndSiblings to avoid the PruneSiblings
        // side effect that disposes the queue's first item's child enumerator,
        // which would prevent already-queued nodes from being visited in BFS.
        nodeTraversalStrategies = NodeTraversalStrategies.PruneSubtree;
      }

      while (true)
      {
        var result = await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

        if (!result)
          return false;

        if (InnerTreenumerator.Mode == TreenumeratorMode.VisitingNode
          && (InnerTreenumerator.VisitCount < 2
          || InnerTreenumerator.Position != Position
          || InnerTreenumerator.VisitCount != VisitCount + 1))
        {
          UpdateState();
          return true;
        }

        nodeTraversalStrategies = NodeTraversalStrategies.PruneSubtree;
      }
    }

    private void UpdateState()
    {
      Mode = InnerTreenumerator.Mode;

      if (!EnumerationFinished)
      {
        Node = InnerTreenumerator.Node;
        VisitCount = InnerTreenumerator.VisitCount;
        Position = InnerTreenumerator.Position;
      }
    }
  }
}
