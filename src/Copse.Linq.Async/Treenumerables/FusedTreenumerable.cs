using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // Type-inference factory: hides the selector-struct spelling at every construction site.
  internal static class FusedTreenumerable
  {
    public static FusedTreenumerable<TSource, TResult, TVerdictSelector> Create<TSource, TResult, TVerdictSelector>(
      IAsyncTreenumerable<TSource> source,
      TVerdictSelector verdictSelector,
      bool containsRelabelingStage)
      where TVerdictSelector : struct, IVerdictSelector<TSource, TResult>
      => new FusedTreenumerable<TSource, TResult, TVerdictSelector>(source, verdictSelector, containsRelabelingStage);
  }

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

    // The bind: the stage carries the composition law, so splicing is one line plus the
    // relabeling fold.
    public IAsyncTreenumerable<TOuterResult> Fuse<TOuterResult>(FusionStage<TResult, TOuterResult> stage)
    {
      var innerVerdictSelector = _VerdictSelector;

      return FusedTreenumerable.Create<TSource, TOuterResult, FuncVerdictSelector<TSource, TOuterResult>>(
        _Source,
        new FuncVerdictSelector<TSource, TOuterResult>(nodeContext =>
          stage.ApplyAfter(innerVerdictSelector.GetVerdict(nodeContext), nodeContext.Position)),
        ContainsRelabelingStage | stage.Relabels);
    }
  }
}
