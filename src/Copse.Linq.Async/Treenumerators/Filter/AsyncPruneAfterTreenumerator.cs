using Copse.Async;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>PruneAfter</c> and the codegen source of truth for its sync twin: strip the
  /// <c>await</c> and it becomes the synchronous driver. Forwards the inner visit stream unchanged
  /// except that a scheduled node matching the predicate keeps its own visit but sheds its subtree
  /// (<see cref="NodeTraversalStrategies.SkipDescendants"/> is added to the pull). Dimension-agnostic.
  /// </summary>
  public sealed class AsyncPruneAfterTreenumerator<TNode>
    : AsyncTreenumeratorWrapper<TNode>
  {
    public AsyncPruneAfterTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (EnumerationFinished)
        return false;

      if (Mode == TreenumeratorMode.SchedulingNode && _Predicate(this.ToNodeContext()))
        nodeTraversalStrategies |= NodeTraversalStrategies.SkipDescendants;

      var result = await InnerTreenumerator.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      UpdateState();

      return result;
    }

    private void UpdateState()
    {
      Mode = InnerTreenumerator.Mode;

      if (!EnumerationFinished)
      {
        Node = InnerTreenumerator.Node;
        VisitCount = InnerTreenumerator.VisitCount;
        Position = InnerTreenumerator.Position;
      }
    }
  }
}
