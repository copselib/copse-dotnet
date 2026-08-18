using Copse.Core.Async;
using Copse.Linq.Async; // the sync transform needs the mapped using to resolve the treenumerator
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // PruneAfter's named wrapper: plain acquisition keeps the bespoke prune-after driver (no
  // promotion machinery -- it only ever sheds whole subtrees below kept nodes). PruneAfter is
  // label-preserving (survivors keep their coordinates), so even positional lambdas compose
  // across it -- in-tier through the light doors, and since the seal opened (2026-08-18)
  // a rejecting operator splices over it through the inherited general Compose.
  internal sealed partial class AsyncPruneAfterTreenumerable<TNode> : IAsyncSelectWhereTreenumerable<TNode>
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

    // The general surface (inherited): light wrappers never relabel.
    public bool Relabels => false;

    // The struct splice (the open seal): the predicate rides its own struct leaf -- this
    // wrapper's donation is delegate-free plumbing (one leaf lambda, as always).
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult, TOuterSelector>(
      TOuterSelector outerSelector,
      bool relabels)
      where TOuterSelector : struct, IResultSelector<TNode, TOuterResult>
    {
      return new AsyncSelectWhereTreenumerable<TNode, TOuterResult, ComposedResultSelector<TNode, TNode, TOuterResult, PruneAfterResultSelector<TNode>, TOuterSelector>>(
        _Source,
        new ComposedResultSelector<TNode, TNode, TOuterResult, PruneAfterResultSelector<TNode>, TOuterSelector>(
          new PruneAfterResultSelector<TNode>(_Predicate), outerSelector),
        relabels);
    }

    // The Func splice (inherited): the struct splice with the closure as its one leaf.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => Compose<TOuterResult, FuncResultSelector<TNode, TOuterResult>>(
        new FuncResultSelector<TNode, TOuterResult>(resultSelector), relabels);

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
