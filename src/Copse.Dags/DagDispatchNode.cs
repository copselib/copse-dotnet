using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// A node decorated by a dispatch pass: the source value plus every inflow the pass delivered
  /// to it. Downward (<see cref="Dagnumerable.SourcefixDispatch"/>): one per live in-edge, in
  /// discovery order, sources carrying the single seeded inflow on a default edge. Upward
  /// (<see cref="Dagnumerable.SinkfixDispatch"/>): one per live out-edge, in the pass's arrival
  /// order, sinks receiving none. The dispatch result DECORATES rather than replaces, so
  /// downstream operators choose their view:
  /// <c>.Select(dispatchNode =&gt; ...)</c> for the values, or read <see cref="Inflows"/> for the
  /// attribution.
  /// </summary>
  public sealed class DagDispatchNode<TNode, TDispatch, TEdge>
  {
    internal DagDispatchNode(TNode value, IReadOnlyList<DagInflow<TDispatch, TEdge>> inflows, bool isSource)
    {
      Value = value;
      Inflows = inflows;
      IsSource = isSource;
    }

    public TNode Value { get; }
    public IReadOnlyList<DagInflow<TDispatch, TEdge>> Inflows { get; }

    /// <summary>True for the walk's sources (no live in-edges) -- where a downward pass seeds and an upward attribution terminates.</summary>
    public bool IsSource { get; }
  }
}
