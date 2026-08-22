namespace Copse.Dags
{
  /// <summary>
  /// One ARRIVAL -- the arrival protocol's atom (design-docs/DAG_CONTRACT_DESIGN.md, the arrival
  /// protocol): a node reached together with the in-edge that brought
  /// you, relative to the walk's orientation. Carries the dispatching far endpoint (value and
  /// ordinal -- provenance from the API, never smuggled in payloads) and the edge payload. The
  /// arrival unifies what the visit protocol spells five ways (inflows, dispatch inflows, edge
  /// contexts, seeded inflows, conventional discoveries); a source's event simply has no
  /// arrivals -- no synthetic-boundary convention needed.
  /// </summary>
  public readonly struct DagArrival<TNode, TEdge>
  {
    internal DagArrival(int dispatcherOrdinal, TNode dispatcher, TEdge edge)
    {
      DispatcherOrdinal = dispatcherOrdinal;
      Dispatcher = dispatcher;
      Edge = edge;
    }

    /// <summary>The dispatching far endpoint's ordinal -- the correlation key to its own event.</summary>
    public int DispatcherOrdinal { get; }

    /// <summary>The dispatching far endpoint's value.</summary>
    public TNode Dispatcher { get; }

    /// <summary>The edge payload the arrival rode in on.</summary>
    public TEdge Edge { get; }
  }
}
