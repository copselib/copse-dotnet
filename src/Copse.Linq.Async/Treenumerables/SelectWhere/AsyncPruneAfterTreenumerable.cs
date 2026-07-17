using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneAfter's named wrapper: plain acquisition keeps the bespoke prune-after driver (no
  // promotion machinery -- it only ever sheds whole subtrees below kept nodes), and composability
  // costs one property: PruneAfter is label-preserving (survivors keep their coordinates), so
  // its map carries relabeling: false and even positional lambdas may compose across it.
  internal sealed class AsyncPruneAfterTreenumerable<TNode> : IAsyncSelectWhereTreenumerable<TNode>
  {
    public AsyncPruneAfterTreenumerable(
      IAsyncTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    // PruneAfter is label-preserving: survivors keep their coordinates.
    public bool Relabels => false;

    // PruneAfter over PruneAfter stays on the bespoke driver: both are label-preserving and
    // neither can reject, so the pair merges into ONE wrapper by predicate union -- prune
    // when either matches. Inner-first short-circuit preserves per-node lambda order; the
    // outer predicate skips nodes the inner already matched, which the purity contract
    // permits (counts unspecified). The general path would convert to the Where driver and
    // pay promotion machinery a never-rejecting chain cannot use.
    internal AsyncPruneAfterTreenumerable<TNode> MergePruneAfter(Func<NodeContext<TNode>, bool> outerPredicate)
    {
      var innerPredicate = _Predicate;

      return new AsyncPruneAfterTreenumerable<TNode>(
        _Source, nodeContext => innerPredicate(nodeContext) || outerPredicate(nodeContext));
    }

    // PruneAfter's selector, stated once (the operator's compose branches use this too): keep
    // the node; a match sheds its subtree.
    internal static Func<NodeContext<TNode>, SelectWhereResult<TNode>> CreateResultSelector(Func<NodeContext<TNode>, bool> predicate)
      => nodeContext => new SelectWhereResult<TNode>(
        nodeContext.Node,
        predicate(nodeContext)
          ? NodeTraversalStrategies.SkipDescendants
          : NodeTraversalStrategies.TraverseAll);

    // Composition converts to the general representation and composes there (unwrap, discard,
    // rebuild); plain acquisition below keeps the bespoke driver and never pays this.
    private SelectWhereTreenumerable<TNode, TNode, FuncResultSelector<TNode, TNode>> ToSelectWhere()
      => new SelectWhereTreenumerable<TNode, TNode, FuncResultSelector<TNode, TNode>>(
        _Source, new FuncResultSelector<TNode, TNode>(CreateResultSelector(_Predicate)), relabels: false);

    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => ToSelectWhere().Compose(resultSelector, relabels);

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
