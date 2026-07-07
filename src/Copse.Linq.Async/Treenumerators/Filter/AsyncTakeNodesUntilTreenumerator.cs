using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>TakeNodesUntil</c>: the direct-style async port of
  /// <c>Copse.Linq.Treenumerators.TakeNodesUntilTreenumerator</c>. Forwards the inner (async) visit
  /// stream until a scheduled node matches the predicate; from there scheduling stops (the matched
  /// node's subtree and later siblings are pruned), optionally keeping the matched node itself
  /// (<paramref name="keepFinalNode"/>). Dimension-agnostic; the only seam is the awaited inner pull.
  ///
  /// <para><b>This is the codegen source of truth for the sync TakeNodesUntil twin.</b> Strip the
  /// <c>await</c>s and it becomes the synchronous driver.</para>
  /// </summary>
  public sealed class AsyncTakeNodesUntilTreenumerator<TNode>
    : IAsyncTreenumerator<TNode>
  {
    public AsyncTakeNodesUntilTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate,
      bool keepFinalNode)
    {
      _Inner = innerTreenumeratorFactory();
      _Predicate = predicate;
      _KeepFinalNode = keepFinalNode;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;
    private bool _KeepFinalNode;
    private bool _StopSchedulingNodes;
    private bool _FinalVisitRemaining = false;

    private bool _Finished;

    public TNode Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      var moved = await OnMoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!moved)
        _Finished = true;

      return moved;
    }

    private async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_StopSchedulingNodes)
        return await OnSchedulingStoppedAsync(nodeTraversalStrategies).ConfigureAwait(false);

      if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      if (_Inner.Mode == TreenumeratorMode.VisitingNode)
      {
        UpdateState();
        return true;
      }

      if (_Predicate(new NodeContext<TNode>(_Inner.Node, _Inner.Position)))
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
        nodeTraversalStrategies |= NodeTraversalStrategies.SkipDescendantsAndSiblings;
      }
      else
      {
        // SkipNodeAndDescendants rather than SkipAll: SkipSiblings would dispose the queue front's
        // child enumerator, stranding already-queued nodes in BFS.
        nodeTraversalStrategies = NodeTraversalStrategies.SkipNodeAndDescendants;
      }

      while (true)
      {
        var result = await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

        if (!result)
          return false;

        if (_Inner.Mode == TreenumeratorMode.VisitingNode
          && (_Inner.VisitCount < 2
          || _Inner.Position != Position
          || _Inner.VisitCount != VisitCount + 1))
        {
          UpdateState();
          return true;
        }

        nodeTraversalStrategies = NodeTraversalStrategies.SkipNodeAndDescendants;
      }
    }

    private void UpdateState()
    {
      Mode = _Inner.Mode;

      if (!_Finished)
      {
        Node = _Inner.Node;
        VisitCount = _Inner.VisitCount;
        Position = _Inner.Position;
      }
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
