using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The survey-shaped UPWARD pass -- per-owner attribution through shared entities, the
    /// diamond's anti-double-count (each node decides what travels up EACH in-edge, so what a
    /// child sent up an edge IS that parent's share, by construction). Sourcefix-of-the-
    /// transpose, served by the same survey core read upward (the derivation
    /// ruling). Destructured seats: each node with live in-edges is surveyed once, in
    /// readiness order (children's upflows complete first), receiving its value, its
    /// edge-paired upflow arrivals (the children's writes, in out-edge order; empty at sinks
    /// -- value originates IN the nodes, so the pass runs UNSEEDED and no virtual family
    /// fires; the seed flavor is ruled lawful but deferred, so this signature is deliberately
    /// fixer-less: explicit type arguments, accepted with eyes open), and one exactly-once
    /// target per IN-edge (parent value, payload; discovery order). Sources are never
    /// surveyed; their arrival groups in the result ARE the attribution. Returns the survey
    /// tier's pairing over the source's shared structure, in the source's own orientation.
    /// </summary>
    public static DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> SinkfixDispatch<TNode, TDispatch, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      return DispatchBuffer(source.Materialize(), seeded: false, default, DagFlowOrientation.Sinkfix, survey);
    }
  }
}
