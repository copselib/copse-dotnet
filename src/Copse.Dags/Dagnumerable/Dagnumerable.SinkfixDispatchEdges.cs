using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-result flavor of the upward survey pass (the edge dual, tier 2) -- the
    /// GROUP-scoped edge writer: what each survey dispatches BECOMES the result's edge
    /// payloads. Sourcefix-of-the-transpose, served by the same edge-writer core read upward
    /// (the 2026-08-05 derivation ruling). Destructured seats: every node with in-edges is
    /// surveyed once, in readiness order, receiving its value, its OUT-edges'
    /// already-rewritten payloads as arrivals (the children's writes -- the cascade; empty at
    /// sinks), and one exactly-once target per IN-edge (parent value, old payload; discovery
    /// order). Sources are never surveyed, yet every edge is written exactly once: each edge
    /// is precisely one non-source node's in-edge. A node's in-edge group is a constrained
    /// unit (a distribution, where payloads are weights) and rewriting it demands the whole
    /// group in scope -- conditioning, rebalancing, normalization are all one survey lambda;
    /// the caller owns the payload algebra, the library owns completeness, order, and
    /// strictness. No boundary, no seed (no virtual edges exist to rewrite): deliberately
    /// fixer-less. Returns node values unchanged over the same shape with payloads replaced.
    /// </summary>
    public static DagBuffer<TNode, TEdgeResult> SinkfixDispatchEdges<TNode, TEdge, TEdgeResult>(
      this IDagnumerable<TNode, TEdge> source,
      DagDispatchSurvey<TNode, TEdgeResult, TEdge> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      return DispatchEdgesBuffer(source.Materialize(), DagFlowOrientation.Sinkfix, survey);
    }
  }
}
