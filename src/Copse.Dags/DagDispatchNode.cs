using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// A node decorated by <see cref="Dagnumerable.RootfixDispatch"/>: the source value plus every
  /// inflow the pass delivered to it -- one per live in-edge, paired with its edge payload, in
  /// discovery order (a source node carries the single seeded inflow on a default edge). The
  /// dispatch result DECORATES rather than replaces, so downstream operators choose their view:
  /// <c>.Select(dispatchNode =&gt; ...)</c> for the values, or read <see cref="Inflows"/> for the
  /// attribution.
  /// </summary>
  public sealed class DagDispatchNode<TNode, TDispatch, TEdge>
  {
    internal DagDispatchNode(TNode value, IReadOnlyList<DagInflow<TDispatch, TEdge>> inflows)
    {
      Value = value;
      Inflows = inflows;
    }

    public TNode Value { get; }
    public IReadOnlyList<DagInflow<TDispatch, TEdge>> Inflows { get; }
  }
}
