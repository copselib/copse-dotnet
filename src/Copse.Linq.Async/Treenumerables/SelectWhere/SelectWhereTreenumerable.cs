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
  internal sealed class SelectWhereTreenumerable<TSource, TResult, TResultSelector> : IAsyncSelectWhereTreenumerable<TResult>
    where TResultSelector : struct, IResultSelector<TSource, TResult>
  {
    public SelectWhereTreenumerable(
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

    // The composition law, written once, covering the whole stage algebra (a projection is a
    // stage that never rejects): the fold stops at the first SkipNode-carrying result (that
    // node left the logical tree, so later stages never saw it in the stacked pipeline, and
    // it has no outer value); while accepting, the value maps and strategies union.
    public IAsyncTreenumerable<TOuterResult> Compose<TOuterResult>(
      Func<NodeContext<TResult>, SelectWhereResult<TOuterResult>> stage,
      bool relabels)
    {
      var resultSelector = _ResultSelector;

      return new SelectWhereTreenumerable<TSource, TOuterResult, FuncResultSelector<TSource, TOuterResult>>(
        _Source,
        new FuncResultSelector<TSource, TOuterResult>(nodeContext =>
        {
          var innerResult = resultSelector.GetResult(nodeContext);

          if (innerResult.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode))
            return new SelectWhereResult<TOuterResult>(default, innerResult.Strategies);

          var stageResult = stage(new NodeContext<TResult>(innerResult.Value, nodeContext.Position));

          return new SelectWhereResult<TOuterResult>(stageResult.Value, stageResult.Strategies | innerResult.Strategies);
        }),
        ContainsRelabelingStage | relabels);
    }
  }
}
