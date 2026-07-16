using Copse.Core;
using Copse.Core.Async;
using System;

namespace Copse.Linq.Async.Treenumerables
{
  // The accumulated Kleisli arrow of a fused chain, reified with its combinators: the source,
  // the composed mapping (kept in its RAWEST form -- a projection-only chain retains the bare
  // composed selector so reification can stay on the light Select treenumerator; the first
  // filter converts the representation to verdict shape), and the relabeling bit. All fusion
  // algebra lives here: the composition law (first reject stops, accept-side strategies
  // union), the purity tracking, and the representation choice.
  internal sealed class CompositionMap<TSource, TNode> : ICompositionMap<TNode>
  {
    private CompositionMap(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TNode> projection,
      Func<NodeContext<TSource>, CompositionVerdict<TNode>> verdict,
      bool containsRelabelingStage)
    {
      _Source = source;
      _Projection = projection;
      _Verdict = verdict;
      ContainsRelabelingStage = containsRelabelingStage;
    }

    private readonly IAsyncTreenumerable<TSource> _Source;

    // Exactly one is set: the map's representation. Projection-only chains cannot reject and
    // never relabel; verdict-backed chains run the full monad.
    private readonly Func<NodeContext<TSource>, TNode> _Projection;
    private readonly Func<NodeContext<TSource>, CompositionVerdict<TNode>> _Verdict;

    public bool ContainsRelabelingStage { get; }

    public static CompositionMap<TSource, TNode> OfProjection(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, TNode> projection)
      => new CompositionMap<TSource, TNode>(source, projection, null, containsRelabelingStage: false);

    public static CompositionMap<TSource, TNode> OfVerdict(
      IAsyncTreenumerable<TSource> source,
      Func<NodeContext<TSource>, CompositionVerdict<TNode>> verdict,
      bool containsRelabelingStage)
      => new CompositionMap<TSource, TNode>(source, null, verdict, containsRelabelingStage);

    public ICompositionMap<TOuterResult> Select<TOuterResult>(Func<NodeContext<TNode>, TOuterResult> selector)
    {
      if (_Projection != null)
      {
        var innerProjection = _Projection;

        return CompositionMap<TSource, TOuterResult>.OfProjection(
          _Source,
          nodeContext => selector(new NodeContext<TNode>(innerProjection(nodeContext), nodeContext.Position)));
      }

      var innerVerdict = _Verdict;

      return CompositionMap<TSource, TOuterResult>.OfVerdict(
        _Source,
        nodeContext =>
        {
          var verdict = innerVerdict(nodeContext);

          // A rejected node has no outer value -- the selector never sees it (the stacked
          // pipeline's Select layer never received the node).
          return verdict.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode)
            ? new CompositionVerdict<TOuterResult>(default, verdict.Strategies)
            : new CompositionVerdict<TOuterResult>(
                selector(new NodeContext<TNode>(verdict.Value, nodeContext.Position)),
                verdict.Strategies);
        },
        ContainsRelabelingStage);
    }

    public ICompositionMap<TNode> Filter(Func<NodeContext<TNode>, CompositionVerdict<TNode>> stage, bool relabels)
    {
      if (_Projection != null)
      {
        var innerProjection = _Projection;

        return CompositionMap<TSource, TNode>.OfVerdict(
          _Source,
          nodeContext => stage(new NodeContext<TNode>(innerProjection(nodeContext), nodeContext.Position)),
          relabels);
      }

      var innerVerdict = _Verdict;

      return CompositionMap<TSource, TNode>.OfVerdict(
        _Source,
        nodeContext =>
        {
          var verdict = innerVerdict(nodeContext);

          // The composition law: the fold stops once SkipNode is aboard (the node left the
          // logical tree, so later stages never saw it in the stacked pipeline); accept-side
          // strategies union.
          return verdict.Strategies.HasNodeTraversalStrategies(NodeTraversalStrategies.SkipNode)
            ? verdict
            : stage(new NodeContext<TNode>(verdict.Value, nodeContext.Position)).WithEarlierStrategies(verdict.Strategies);
        },
        ContainsRelabelingStage | relabels);
    }

    public IAsyncTreenumerable<TNode> ToTreenumerable()
      => _Projection != null
        ? (IAsyncTreenumerable<TNode>)new AsyncSelectTreenumerable<TSource, TNode>(_Source, _Projection)
        : new ComposableTreenumerable<TSource, TNode, FuncVerdictSelector<TSource, TNode>>(
            _Source, new FuncVerdictSelector<TSource, TNode>(_Verdict), ContainsRelabelingStage);
  }
}
