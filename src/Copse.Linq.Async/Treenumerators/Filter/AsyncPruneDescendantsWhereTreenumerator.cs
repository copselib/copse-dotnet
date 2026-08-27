using Copse;
using Copse.Core;
using Copse.Linq.Extensions;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerators
{
  /// <summary>
  /// <b>async</b> <c>PruneDescendantsWhere</c> and the codegen source of truth for its sync twin: strip the
  /// <c>await</c> and it becomes the synchronous driver. Forwards the inner visit stream unchanged
  /// except that a scheduled node matching the predicate keeps its own visit but sheds its subtree
  /// (<see cref="NodeTraversalStrategies.PruneDescendants"/> is added to the pull). Dimension-agnostic.
  /// </summary>
  internal sealed class AsyncPruneDescendantsWhereTreenumerator<TNode>
    : AsyncTreenumeratorWrapper<TNode>
  {
    public AsyncPruneDescendantsWhereTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeAndPosition<TNode>, bool> predicate)
      : base(innerTreenumeratorFactory)
    {
      _Predicate = predicate;
    }

    private readonly Func<NodeAndPosition<TNode>, bool> _Predicate;

    protected override async ValueTask<bool> OnMoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (EnumerationFinished)
        return false;

      // Never test the pre-enumeration sentinel (ForestRoot convention: default node, mode
      // SchedulingNode): user lambdas see real nodes only.
      if (Mode == TreenumeratorMode.SchedulingNode && !Position.IsForestRoot && _Predicate(this.ToNodeAndPosition()))
        nodeTraversalStrategies |= NodeTraversalStrategies.PruneDescendants;

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
