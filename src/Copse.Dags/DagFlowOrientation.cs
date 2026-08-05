namespace Copse.Dags
{
  // The fold direction over a buffer's structure. ONE core serves each operator pair (scan,
  // dispatch, dispatch-edges): sinkfix IS sourcefix-of-the-transpose (the 2026-08-05 ruling
  // -- the transpose LAW is pinned semantically by the coherence battery), and the
  // orientation flag is how one implementation reads the transpose WITHOUT materializing it
  // -- which also keeps per-group order a STRUCTURAL fact (sinkfix arrivals in OUT-EDGE
  // order, sinkfix targets in discovery order; a literal transpose walk would present
  // arrival groups in reverse-topological child order instead -- the per-group order trap
  // the ratification flagged for verification, verified and dodged here).
  internal enum DagFlowOrientation
  {
    Sourcefix,
    Sinkfix,
  }
}
