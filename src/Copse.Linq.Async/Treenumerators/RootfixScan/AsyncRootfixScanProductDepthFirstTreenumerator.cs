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
  /// The COMPOSED-PROJECTION twin of <see cref="AsyncRootfixScanDepthFirstTreenumerator{TNode, TAccumulate}"/>
  /// (the streaming projection citizenship, design-docs/SELECT_INTO_CAPTURES_DESIGN.md): the
  /// scan state threads exactly as the plain engine's -- pair-shaped visits, the same
  /// accumulator adapters -- and the PRODUCT is <c>productSelector(pair)</c> applied at
  /// emission, so a composed <c>Select</c> costs one selector call inside this engine instead
  /// of a whole wrapper layer per pull. Visiting re-emissions re-apply the selector (pure by
  /// Select's documented contract; invocation count is deliberately unspecified). The plain
  /// engine stays untouched -- the plain spelling never pays for this seam.
  /// </summary>
  internal sealed class AsyncRootfixScanProductDepthFirstTreenumerator<TNode, TAccumulate, TProduct>
    : AsyncTreenumeratorWrapper<TNode, TProduct>
  {
    public AsyncRootfixScanProductDepthFirstTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<NodeAccumulation<TNode, TAccumulate>>, NodeContext<TNode>, NodeAccumulation<TNode, TAccumulate>> accumulator,
      NodeAccumulation<TNode, TAccumulate> seed,
      Func<NodeAccumulation<TNode, TAccumulate>, TProduct> productSelector) : base(innerTreenumeratorFactory)
    {
      _Accumulator = accumulator;
      _ProductSelector = productSelector;

      var seedVisit =
        new NodeVisit<NodeAccumulation<TNode, TAccumulate>>(
          TreenumeratorMode.VisitingNode,
          seed,
          1,
          NodePosition.ForestRoot);

      _Stack.Push(seedVisit);
    }

    private readonly Func<NodeContext<NodeAccumulation<TNode, TAccumulate>>, NodeContext<TNode>, NodeAccumulation<TNode, TAccumulate>> _Accumulator;
    private readonly Func<NodeAccumulation<TNode, TAccumulate>, TProduct> _ProductSelector;

    private readonly Stack<NodeVisit<NodeAccumulation<TNode, TAccumulate>>> _Stack = new Stack<NodeVisit<NodeAccumulation<TNode, TAccumulate>>>();
    private readonly Stack<NodeVisit<NodeAccumulation<TNode, TAccumulate>>> _SkippedStack = new Stack<NodeVisit<NodeAccumulation<TNode, TAccumulate>>>();

    private Stack<NodeVisit<NodeAccumulation<TNode, TAccumulate>>> GetStackWithDeepestNodeVisit()
    {
      if (_SkippedStack.Count > 0
        && _SkippedStack.Peek().Position.Depth > _Stack.Peek().Position.Depth)
      {
        return _SkippedStack;
      }

      return _Stack;
    }

    private int GetDeepestSeenDepth() => GetStackWithDeepestNodeVisit().Peek().Position.Depth;

    private NodeVisit<NodeAccumulation<TNode, TAccumulate>> PopStackWithDeepestNodeVisit() => GetStackWithDeepestNodeVisit().Pop();

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      // Strategies on the very first MoveNext apply to no node (the inner is still parked at the
      // pre-enumeration forest root, whose contractual Mode is SchedulingNode) -- the engine
      // ignores them, and so must the skip bookkeeping, or a SkipNode-driving consumer (e.g.
      // GetPreorderTraversal) pops the seed sentinel as if it were a scheduled node.
      if (InnerTreenumerator.Mode == TreenumeratorMode.SchedulingNode
        && !InnerTreenumerator.Position.IsForestRoot
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
        new NodeVisit<NodeAccumulation<TNode, TAccumulate>>(
          InnerTreenumerator.Mode,
          node,
          InnerTreenumerator.VisitCount,
          InnerTreenumerator.Position);

      _Stack.Push(newVisit);

      UpdateStateFromNodeVisit(newVisit);

      return true;
    }

    private void UpdateStateFromNodeVisit(NodeVisit<NodeAccumulation<TNode, TAccumulate>> nodeVisit)
    {
      Mode = nodeVisit.Mode;
      Node = _ProductSelector(nodeVisit.Node);
      VisitCount = nodeVisit.VisitCount;
      Position = nodeVisit.Position;
    }
  }
}
