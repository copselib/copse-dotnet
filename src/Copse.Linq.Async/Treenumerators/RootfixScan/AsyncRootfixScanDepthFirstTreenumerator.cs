using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// Depth-first <b>async</b> <c>RootfixScan</c>: the direct-style async port of
  /// <c>Copse.Linq.Treenumerators.RootfixScanDepthFirstTreenumerator</c>. A cumulative scan from the
  /// root -- each scheduled node's value is the accumulator applied to its parent's accumulated value
  /// -- transforming the inner (async) <c>TNode</c> stream into a <c>TAccumulate</c> stream. The only
  /// seam is the awaited inner pull; all accumulation state is synchronous stacks.
  ///
  /// <para><b>This is the codegen source of truth for the sync RootfixScan (DFT) twin.</b> Strip the
  /// <c>await</c> and it becomes the synchronous driver.</para>
  /// </summary>
  public sealed class AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>
    : IAsyncTreenumerator<TAccumulate>
  {
    public AsyncRootfixScanDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed)
    {
      _Inner = innerTreenumeratorFactory();
      _Accumulator = accumulator;

      var seedVisit =
        new NodeVisit<TAccumulate>(
          TreenumeratorMode.VisitingNode,
          seed,
          1,
          NodePosition.ForestRoot);

      _Stack.Push(seedVisit);
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;

    private readonly Stack<NodeVisit<TAccumulate>> _Stack = new Stack<NodeVisit<TAccumulate>>();
    private readonly Stack<NodeVisit<TAccumulate>> _SkippedStack = new Stack<NodeVisit<TAccumulate>>();

    private bool _Finished;

    public TAccumulate Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    private Stack<NodeVisit<TAccumulate>> GetStackWithDeepestNodeVisit()
    {
      if (_SkippedStack.Count > 0
        && _SkippedStack.Peek().Position.Depth > _Stack.Peek().Position.Depth)
      {
        return _SkippedStack;
      }

      return _Stack;
    }

    private int GetDeepestSeenDepth() => GetStackWithDeepestNodeVisit().Peek().Position.Depth;

    private NodeVisit<TAccumulate> PopStackWithDeepestNodeVisit() => GetStackWithDeepestNodeVisit().Pop();

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
      if (_Inner.Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _SkippedStack.Push(_Stack.Pop());
      }

      if (!await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      var currentDepth = _Inner.Position.Depth;

      if (_Inner.Mode == TreenumeratorMode.SchedulingNode)
      {
        while (GetDeepestSeenDepth() >= currentDepth)
          PopStackWithDeepestNodeVisit();
      }
      else
      {
        while (_Stack.Peek().Position.Depth > currentDepth)
          PopStackWithDeepestNodeVisit();
      }

      var node =
        _Inner.Mode == TreenumeratorMode.SchedulingNode
        ? _Accumulator(GetStackWithDeepestNodeVisit().Peek().ToNodeContext(), new NodeContext<TNode>(_Inner.Node, _Inner.Position))
        : _Stack.Pop().Node;

      var newVisit =
        new NodeVisit<TAccumulate>(
          _Inner.Mode,
          node,
          _Inner.VisitCount,
          _Inner.Position);

      _Stack.Push(newVisit);

      UpdateStateFromNodeVisit(newVisit);

      return true;
    }

    private void UpdateStateFromNodeVisit(NodeVisit<TAccumulate> nodeVisit)
    {
      Mode = nodeVisit.Mode;
      Node = nodeVisit.Node;
      VisitCount = nodeVisit.VisitCount;
      Position = nodeVisit.Position;
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
