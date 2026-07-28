using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// The arrival protocol's ONE element kind (docs/DAG_CONTRACT_DESIGN.md, the arrival
  /// protocol): a node's single event, fired once per live node in topological order, carrying
  /// the complete in-arrival group (empty at sources), the node, and the out-departure group
  /// (empty at sinks) -- the CSR row as a stream element. This is the shape every survey pass
  /// already consumes; the Discover/Enter interleaving survives as walk bookkeeping, not
  /// public protocol. Verdicts (sever/suppress on the owning
  /// <see cref="IArrivalDagnumerator{TNode, TEdge}"/>) answer THIS event until the next
  /// advance; the event itself cannot be retracted.
  /// </summary>
  public sealed class DagNodeEvent<TNode, TEdge>
  {
    internal DagNodeEvent(
      int ordinal,
      TNode value,
      IReadOnlyList<DagArrival<TNode, TEdge>> arrivals,
      IReadOnlyList<DagDeparture<TNode, TEdge>> departures,
      bool isSource)
    {
      Ordinal = ordinal;
      Value = value;
      Arrivals = arrivals;
      Departures = departures;
      IsSource = isSource;
    }

    /// <summary>The node's correlation key: the source walk's ordinal, preserved (gaps legal).</summary>
    public int Ordinal { get; }

    public TNode Value { get; }

    /// <summary>The complete live in-arrival group, in the dispatchers' event order.</summary>
    public IReadOnlyList<DagArrival<TNode, TEdge>> Arrivals { get; }

    /// <summary>The out-departure group, in out-edge order.</summary>
    public IReadOnlyList<DagDeparture<TNode, TEdge>> Departures { get; }

    /// <summary>True for a structural source -- distinguishable from a node whose arrivals were all severed upstream (which never events at all).</summary>
    public bool IsSource { get; }

    /// <summary>True when the node has no departures to propose.</summary>
    public bool IsSink => Departures.Count == 0;
  }
}
