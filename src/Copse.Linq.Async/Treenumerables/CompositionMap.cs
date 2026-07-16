using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The accumulated Kleisli arrow of a composed chain, reified with its combinators: the source,
  // the composed mapping (kept in its RAWEST form -- a projection-only chain retains the bare
  // composed selector so reification can stay on the light Select treenumerator; the first
  // filter converts the representation to result shape), and the relabeling bit. All
  // composition algebra lives here: the composition law (first reject stops, accept-side strategies
  // union), the purity tracking, and the representation choice.
  internal sealed class CompositionMap<TSource, TNode> : ICompositionMap<TNode>
  {
    private CompositionMap(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TNode> projection,
      Func<NodeContext<TSource>, CompositionResult<TNode>> result,
      bool containsRelabelingStage)
    {
      _Source = source;
      _Projection = projection;
      _Result = result;
      ContainsRelabelingStage = containsRelabelingStage;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;

    // Exactly one is set: the map's representation. Projection-only chains cannot reject and
    // never relabel; result-backed chains run the full monad.
    private readonly Func<NodeContext<TSource>, TNode> _Projection;
    private readonly Func<NodeContext<TSource>, CompositionResult<TNode>> _Result;

    public bool ContainsRelabelingStage { get; }

    public static CompositionMap<TSource, TNode> OfProjection(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TNode> projection)
      => new CompositionMap<TSource, TNode>(source, projection, null, containsRelabelingStage: false);

    public static CompositionMap<TSource, TNode> OfResult(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, CompositionResult<TNode>> result,
      bool containsRelabelingStage)
      => new CompositionMap<TSource, TNode>(source, null, result, containsRelabelingStage);

    public ICompositionMap<TOuterResult> Select<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
    {
      if (_Projection != null)
      {
        var innerProjection = _Projection;

        return CompositionMap<TSource, TOuterResult>.OfProjection(
          _Source,
          nodeContext => selector(new NodeContext<TNode>(innerProjection(nodeContext), nodeContext.Position)));
      }

      var innerResult = _Result;

      return CompositionMap<TSource, TOuterResult>.OfResult(
        _Source,
        nodeContext =>
        {
          var result = innerResult(nodeContext);

          // A rejected node has no outer value -- the selector never sees it (the stacked
          // pipeline's Select layer never received the node).
          return result.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode)
            ? new CompositionResult<TOuterResult>(default, result.Strategies)
            : new CompositionResult<TOuterResult>(
                selector(new NodeContext<TNode>(result.Value, nodeContext.Position)),
                result.Strategies);
        },
        ContainsRelabelingStage);
    }

    public ICompositionMap<TNode> Filter(Func<NodeContext<TNode>, CompositionResult<TNode>> stage, bool relabels)
    {
      if (_Projection != null)
      {
        var innerProjection = _Projection;

        return CompositionMap<TSource, TNode>.OfResult(
          _Source,
          nodeContext => stage(new NodeContext<TNode>(innerProjection(nodeContext), nodeContext.Position)),
          relabels);
      }

      var innerResult = _Result;

      return CompositionMap<TSource, TNode>.OfResult(
        _Source,
        nodeContext =>
        {
          var result = innerResult(nodeContext);

          // The composition law: the fold stops once SkipNode is aboard (the node left the
          // logical tree, so later stages never saw it in the stacked pipeline); accept-side
          // strategies union.
          return result.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode)
            ? result
            : stage(new NodeContext<TNode>(result.Value, nodeContext.Position)).WithEarlierStrategies(result.Strategies);
        },
        ContainsRelabelingStage | relabels);
    }

    public IAsyncTreenumerable<TNode> ToTreenumerable()
      => _Projection != null
        ? (IAsyncTreenumerable<TNode>)new AsyncSelectTreenumerable<TSource, TNode>(_Source, _Projection)
        : new ComposableTreenumerable<TSource, TNode, FuncResultSelector<TSource, TNode>>(
            _Source, new FuncResultSelector<TSource, TNode>(_Result), ContainsRelabelingStage);
  }
}
