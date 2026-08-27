using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // TakeSubtreesWhere's composite result: a streaming-tier citizen CARRYING the dimension
  // dispatch (the honest-streaming-baseline rule). The recipe is (source,
  // context predicate); each acquisition constructs that dimension's leanest streaming
  // machinery -- depth-first the bespoke O(1) contiguous-segment wrapper, breadth-first the
  // Where machinery in subtree mode (the subtree stage; the scan chain remains the
  // operator's algebraic DEFINITION, spelled in GetBreadthFirstChain for the product
  // variant). Because the DISPATCH lives behind the citizenship, the operator is not a
  // composition seam: a following Select composes here (the product variant), a following
  // Where joins the one driver over this citizen -- the machinery choice stays an
  // acquisition-time fact, invisible to the algebra.
  internal sealed class AsyncTakeSubtreesWhereTreenumerable<TNode>
    : IAsyncSelectTreenumerable<TNode>
  {
    public AsyncTakeSubtreesWhereTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator()
      => new AsyncTakeSubtreesWhereTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);

    // THE SUBTREE STAGE: the BFT arm is the Where machinery itself in subtree
    // mode -- one wrapper, no scan engine, no pair, the kept-region fact read off the skip
    // prefix the machinery already carries. The scan chain (GetBreadthFirstChain) remains the
    // operator's algebraic definition and the product variant's route.
    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator()
    {
      var predicate = _Predicate;

      return new AsyncWhereBreadthFirstTreenumerator<TNode, TNode, FuncResultSelector<TNode, TNode>>(
        _Source.GetAsyncBreadthFirstTreenumerator,
        new FuncResultSelector<TNode, TNode>(nodeContext => new SelectWhereResult<TNode>(
          nodeContext.Node,
          predicate(nodeContext) ? NodeTraversalStrategies.TraverseAll : NodeTraversalStrategies.SkipNode)),
        takeSubtrees: true);
    }

    public IAsyncSelectTreenumerable<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector)
      => new AsyncTakeSubtreesWhereProductTreenumerable<TNode, TResult>(_Source, _Predicate, selector);

    // The scan spelling (the operator's definition -- SELECT_INTO_CAPTURES_DESIGN.md section
    // 5): "keep this node" is the rootfix fold fact kept(parent) || predicate(node); the
    // outermost rule is the disjunction's short-circuit. The chain joins into one SelectWhere
    // driver over the scan engine.
    internal static IAsyncTreenumerable<TNode> GetBreadthFirstChain(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
      => new AsyncRootfixScanTreenumerable<TNode, bool>(
          source.GetAsyncDepthFirstTreenumerator,
          source.GetAsyncBreadthFirstTreenumerator,
          (parentContext, nodeContext) => parentContext.Node || predicate(nodeContext),
          false)
        .Where(pair => pair.Accumulate)
        .Select(pair => pair.Node);
  }
}
