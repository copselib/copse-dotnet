namespace Copse.Dags
{
  // The fold direction over a buffer's structure. One engine serves each flow operator:
  // sinkfix IS sourcefix-of-the-transpose (the transpose law, pinned by the coherence
  // battery), and the orientation flag is how one implementation reads the transpose WITHOUT
  // materializing it -- which also keeps per-group order a STRUCTURAL fact (sinkfix arrivals
  // in OUT-EDGE order, sinkfix targets in discovery order; a literal transpose walk would
  // present arrival groups in reverse-topological child order instead).
  internal enum DagFlowOrientation
  {
    Sourcefix,
    Sinkfix,
  }
}
