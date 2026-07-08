using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// Depth-first <b>async</b> <c>RootfixScan</c> and the codegen source of truth for its sync twin:
  /// strip the <c>await</c> on the inner pull and it becomes the synchronous driver. A cumulative
  /// scan from the root -- each scheduled node's value is the accumulator applied to its parent's
  /// accumulated value -- transforming the inner TNode stream into a TAccumulate stream; all
  /// accumulation state is synchronous stacks.
  /// </summary>
  public sealed class AsyncRootfixScanDepthFirstTreenumerator<TNode, TAccumulate>
    : AsyncTreenumeratorWrapper<TNode, TAccumulate>
  {
    public AsyncRootfixScanDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> accumulator,
      TAccumulate seed) : base(innerTreenumeratorFactory)
    {
      _Accumulator = accumulator;

      var seedVisit =
        new NodeVisit<TAccumulate>(
          TreenumeratorMode.VisitingNode,
          seed,
          1,
          NodePosition.ForestRoot);

      _Stack.Push(seedVisit);
    }

    private readonly Func<NodeContext<TAccumulate>, NodeContext<TNode>, TAccumulate> _Accumulator;

    private readonly Stack<NodeVisit<TAccumulate>> _Stack = new Stack<NodeVisit<TAccumulate>>();
    private readonly Stack<NodeVisit<TAccumulate>> _SkippedStack = new Stack<NodeVisit<TAccumulate>>();

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

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        && nodeTraversalStrategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
      {
        _SkippedStack.Push(_Stack.Pop());
      }

      if (!await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      var currentDepth = InnerTreenumerator.Position.Depth;

      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode)
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
        InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        ? _Accumulator(GetStackWithDeepestNodeVisit().Peek().ToNodeContext(), InnerTreenumerator.ToNodeContext())
        : _Stack.Pop().Node;

      var newVisit =
        new NodeVisit<TAccumulate>(
          InnerTreenumerator.Mode,
          node,
          InnerTreenumerator.VisitCount,
          InnerTreenumerator.Position);

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
  }
}
