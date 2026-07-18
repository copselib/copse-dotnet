using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> driver for the light SelectPruneAfter tier and the codegen source of truth for
  /// its sync twin: strip the <c>await</c> and it becomes the synchronous driver. Forwards the
  /// inner visit stream with positions unchanged (the tier never relabels), publishing the
  /// composed selector's value per visit; a scheduling visit's result strategies (SkipDescendants
  /// from prune-after members) are merged into the pull that follows it -- the same protocol
  /// moment the consumer's own strategies for that visit arrive. No promotion machinery, no path
  /// state. Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncSelectPruneAfterTreenumerator<TSource, TNode>
    : AsyncTreenumeratorWrapper<TSource, TNode>
  {
    public AsyncSelectPruneAfterTreenumerator(
      Func<IAsyncTreenumerator<TSource>> innerTreenumeratorFactory,
      Func<NodeContext<TSource>, SelectWhereResult<TNode>> resultSelector)
      : base(innerTreenumeratorFactory)
    {
      _ResultSelector = resultSelector;
    }

    private readonly Func<NodeContext<TSource>, SelectWhereResult<TNode>> _ResultSelector;

    private NodeTraversalStrategies _PendingResultStrategies = NodeTraversalStrategies.TraverseAll;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      nodeTraversalStrategies |= _PendingResultStrategies;
      _PendingResultStrategies = NodeTraversalStrategies.TraverseAll;

      if (!await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false))
        return false;

      Mode = InnerTreenumerator.Mode;
      VisitCount = InnerTreenumerator.VisitCount;
      Position = InnerTreenumerator.Position;

      // A successful pull always lands on a real visit (ForestRoot is pre-enumeration only),
      // so the composed selector sees real nodes only. Evaluated per published visit, like the
      // plain Select wrapper (invocation counts are unspecified; only the scheduling visit's
      // strategies count -- subtrees are shed at scheduling, the PruneAfter contract).
      var result = _ResultSelector(InnerTreenumerator.ToNodeContext());

      Node = result.Value;

      if (Mode == TreenumeratorMode.SchedulingNode)
        _PendingResultStrategies = result.Strategies;

      return true;
    }
  }
}
