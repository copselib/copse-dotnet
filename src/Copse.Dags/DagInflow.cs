namespace Copse.Dags
{
  /// <summary>
  /// One in-edge's contribution to a node during a downward pass: the value that arrived and
  /// the edge payload it arrived on -- the edge-aware pairing the operators' accumulations
  /// receive (the builder's scans took bare value lists; the contract closes that gap).
  /// Inflows arrive in discovery order.
  /// </summary>
  public readonly struct DagInflow<TValue, TEdge>
  {
    public DagInflow(TValue value, TEdge edge)
    {
      Value = value;
      Edge = edge;
    }

    public TValue Value { get; }
    public TEdge Edge { get; }
  }
}
