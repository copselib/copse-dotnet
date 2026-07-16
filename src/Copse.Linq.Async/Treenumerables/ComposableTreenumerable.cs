using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The reified stage chain (docs/OPERATOR_FUSION_DESIGN.md, "the result monad"): one wrapper
  // holding the Kleisli-composed result of every fused stage, so value-lambda chains of any
  // length and order collapse to ONE layer over the source. Plain single-stage operators
  // instantiate with their bespoke selector STRUCT (inlined by the JIT -- zero seam cost);
  // spliced chains carry the composed closure in a FuncResultSelector (fusion inherently
  // holds user delegates). Splicing is total: every legality decision was made outer-side.
  internal sealed class ComposableTreenumerable<TSource, TResult, TResultSelector> : IAsyncComposableTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public ComposableTreenumerable(
      IAsyncTreenumerable<TSource> source,
      TResultSelector resultSelector,
      bool containsRelabelingStage)
    {
      _Source = source;
      _ResultSelector = resultSelector;
      ContainsRelabelingStage = containsRelabelingStage;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly TResultSelector _ResultSelector;

    public bool ContainsRelabelingStage { get; }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncBreadthFirstTreenumerator, _ResultSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TSource, TResult, TResultSelector>(
        _Source.GetAsyncDepthFirstTreenumerator, _ResultSelector);

    // Offer up the internal mapping (materialized on demand: acquisition keeps the zero-cost
    // struct seam; only actual composition pays the delegate hop, and fused paths are
    // delegate-bound anyway).
    public ICompositionMap<TResult> Map
    {
      get
      {
        var resultSelector = _ResultSelector;

        return CompositionMap<TSource, TResult>.OfResult(
          _Source, nodeContext => resultSelector.GetResult(nodeContext), ContainsRelabelingStage);
      }
    }
  }
}
