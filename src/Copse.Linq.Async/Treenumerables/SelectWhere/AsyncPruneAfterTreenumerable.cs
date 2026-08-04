using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneAfter's named wrapper: plain acquisition keeps the bespoke prune-after driver (no
  // promotion machinery -- it only ever sheds whole subtrees below kept nodes). PruneAfter is
  // label-preserving (survivors keep their coordinates), so even positional lambdas compose
  // across it -- but only IN-TIER: this wrapper is sealed against general splices (see the
  // interface's boundary ruling), so a rejecting operator stacks over it.
  internal sealed class AsyncPruneAfterTreenumerable<TNode> : IAsyncSelectPruneAfterTreenumerable<TNode>
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

    // PruneAfter over PruneAfter stays on the bespoke driver: the pair merges into ONE
    // wrapper by predicate union.
    public IAsyncTreenumerable<TNode> ComposePruneAfter(Func<NodeContext<TNode>, bool> outerPredicate)
    {
      return new AsyncPruneAfterTreenumerable<TNode>(
        _Source, SelectWhereComposition.PruneAfterThenPruneAfter(_Predicate, outerPredicate));
    }

    // A projection joins: promote to the middle tier (light passthrough driver), never the
    // filter driver.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterTreenumerable<TNode, TOuterResult>(
        _Source, SelectWhereComposition.PruneAfterThenSelect(_Predicate, selector));
    }

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncBreadthFirstTreenumerator, _Predicate);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
