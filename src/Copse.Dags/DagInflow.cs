namespace Copse.Dags
{
  /// <summary>
  /// One edge's contribution to a node during a directional pass: the value that arrived and
  /// the edge payload it arrived on -- the edge-aware pairing the operators' accumulations
  /// receive (the builder's scans took bare value lists; the contract closes that gap). The
  /// pass direction defines the flow: downward passes deliver parents' values on in-edges (in
  /// discovery order); upward passes deliver children's values on out-edges (in the pass's
  /// arrival order, reverse-topological).
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
