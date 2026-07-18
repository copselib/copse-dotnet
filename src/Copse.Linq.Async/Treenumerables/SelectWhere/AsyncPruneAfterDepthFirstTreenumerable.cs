using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // AsyncPruneAfterTreenumerable's depth-first-only twin: plain acquisition keeps the bespoke
  // prune-after driver; PruneAfter is label-preserving, so the wrapper sits on the light tier
  // and even positional lambdas compose across it.
  internal sealed class AsyncPruneAfterDepthFirstTreenumerable<TNode> : IAsyncSelectPruneAfterDepthFirstTreenumerable<TNode>
  {
    public AsyncPruneAfterDepthFirstTreenumerable(
      IAsyncDepthFirstTreenumerable<TNode> source,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Source = source;
      _Predicate = predicate;
    }

    private readonly IAsyncDepthFirstTreenumerable<TNode> _Source;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    // PruneAfter is label-preserving: survivors keep their coordinates.
    public bool Relabels => false;

    // PruneAfter over PruneAfter stays on the bespoke driver: the pair merges into ONE
    // wrapper by predicate union.
    public IAsyncDepthFirstTreenumerable<TNode> ComposePruneAfter(Func<NodeContext<TNode>, bool> outerPredicate)
    {
      return new AsyncPruneAfterDepthFirstTreenumerable<TNode>(
        _Source, SelectWhereComposition.PruneAfterThenPruneAfter(_Predicate, outerPredicate));
    }

    // A projection joins: promote to the middle tier, never the filter driver.
    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
    {
      return new AsyncSelectPruneAfterDepthFirstTreenumerable<TNode, TOuterResult>(
        _Source, SelectWhereComposition.PruneAfterThenSelect(_Predicate, selector));
    }

    // Composition converts to the general representation and composes there; plain acquisition
    // below keeps the bespoke driver and never pays this. The selector is the operator's
    // semantics, stated once on the wide twin.
    private SelectWhereDepthFirstTreenumerable<TNode, TNode, FuncResultSelector<TNode, TNode>> ToSelectWhere()
      => new SelectWhereDepthFirstTreenumerable<TNode, TNode, FuncResultSelector<TNode, TNode>>(
        _Source,
        new FuncResultSelector<TNode, TNode>(AsyncPruneAfterTreenumerable<TNode>.CreateResultSelector(_Predicate)),
        relabels: false);

    public IAsyncDepthFirstTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TNode>, SelectWhereResult<TOuterResult>> resultSelector,
      bool relabels)
      => ToSelectWhere().Compose(resultSelector, relabels);

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() =>
      new AsyncPruneAfterTreenumerator<TNode>(_Source.GetAsyncDepthFirstTreenumerator, _Predicate);
  }
}
