namespace Copse.Dags
{
  /// <summary>
  /// The edge tier's pairing (THE EDGE-PAIRING AMENDMENT, 2026-08-06,
  /// design-docs/DAG_CONTRACT_DESIGN.md): one edge's original payload with the value the pass
  /// dispatched along it -- what the <c>DispatchEdges</c> twins' buffers hold. The rule that
  /// forces it: PROJECTION REPLACES, AGGREGATION PAIRS. <c>SelectEdges</c> replaces payloads
  /// lawfully (consumer-authored, each output derivable from its own input), but a dispatch's
  /// edge values are FLOW-COMPUTED -- path-cumulative, cascade-dependent -- so the
  /// association between an edge and its computed value must come from the API, assembled
  /// (the machinery already held both mid-pass: the arrival seat pairs new value with old
  /// payload at every survey; this type stops discarding that pairing at the result
  /// boundary).
  ///
  /// ONE shape, no input/output split: an edge is 1-in-1-out, so the value dispatched along
  /// it is simultaneously the tail's outflow and the head's arrival -- <see cref="Accumulate"/>
  /// covers both readings. (Contrast the node side, whose 1-in-n-out asymmetry forced
  /// <see cref="DagScanResult{TNode, TAccumulate}"/> and
  /// <see cref="DagDispatchResult{TNode, TDispatch}"/> apart.) Project
  /// <c>.SelectEdges(e =&gt; e.Edge.Accumulate)</c> when only the computed values should
  /// travel on.
  /// </summary>
  public readonly struct DagEdgeResult<TEdge, TDispatch>
  {
    public DagEdgeResult(TEdge edge, TDispatch accumulate)
    {
      Edge = edge;
      Accumulate = accumulate;
    }

    public readonly TEdge Edge;
    public readonly TDispatch Accumulate;

    public override string ToString() => $"{Edge} <- {Accumulate}";
  }
}
