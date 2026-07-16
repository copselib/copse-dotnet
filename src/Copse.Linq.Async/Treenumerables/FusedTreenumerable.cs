using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The reified stage chain (docs/OPERATOR_FUSION_DESIGN.md, "the verdict monad"): one wrapper
  // holding the Kleisli-composed verdict of every fused stage, so value-lambda chains of any
  // length and order collapse to ONE layer over the source. Plain single-stage operators
  // instantiate with their bespoke selector STRUCT (inlined by the JIT -- zero seam cost);
  // spliced chains carry the composed closure in a FuncVerdictSelector (fusion inherently
  // holds user delegates). Splicing is total: every legality decision was made outer-side.
  internal sealed class FusedTreenumerable<TSource, TResult, TVerdictSelector> : IAsyncFusableTreenumerable<TResult>
    where TVerdictSelector : struct, IVerdictSelector<TSource, TResult>
  {
    public FusedTreenumerable(
      IAsyncTreenumerable<TSource> source,
      TVerdictSelector verdictSelector,
      bool containsRelabelingStage)
    {
      _Source = source;
      _VerdictSelector = verdictSelector;
      ContainsRelabelingStage = containsRelabelingStage;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;
    private readonly TVerdictSelector _VerdictSelector;

    public bool ContainsRelabelingStage { get; }

    public IAsyncTreenumerator<TResult> GetAsyncBreadthFirstTreenumerator() =>
      new AsyncWhereBreadthFirstTreenumerator<TSource, TResult, TVerdictSelector>(
        _Source.GetAsyncBreadthFirstTreenumerator, _VerdictSelector);

    public IAsyncTreenumerator<TResult> GetAsyncDepthFirstTreenumerator() =>
      new AsyncWhereDepthFirstTreenumerator<TSource, TResult, TVerdictSelector>(
        _Source.GetAsyncDepthFirstTreenumerator, _VerdictSelector);

    // Offer up the internal mapping (materialized on demand: acquisition keeps the zero-cost
    // struct seam; only actual composition pays the delegate hop, and fused paths are
    // delegate-bound anyway).
    public IFusionMap<TResult> Map
    {
      get
      {
        var verdictSelector = _VerdictSelector;

        return FusionMap<TSource, TResult>.OfVerdict(
          _Source, nodeContext => verdictSelector.GetVerdict(nodeContext), ContainsRelabelingStage);
      }
    }
  }
}
