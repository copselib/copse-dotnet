using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-result flavor of the downward survey pass -- <see cref="SinkfixDispatchEdges"/>'s
    /// mirror (design-docs/DAG_CONTRACT_DESIGN.md, the edge dual, tier 2): what each survey dispatches
    /// BECOMES the result's edge payloads. Every node with out-edges is surveyed once, in
    /// readiness order, with the destructured seats: its value, its IN-edges'
    /// already-rewritten payloads as edge-paired arrivals (ancestors' cascade; empty at
    /// sources; the old payload rides as the arrival's Edge, the dispatching parent as its
    /// Dispatcher), and one exactly-once <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/>
    /// per OUT-edge (child value, old payload; out-edge order). Sinks are never surveyed, yet
    /// every edge is written exactly once: each edge is precisely one non-sink node's
    /// out-edge. The natural home of path-cumulative edge values; the upward twin owns in-edge
    /// group algebra. There is NO boundary invocation and no seed -- a virtual family has no
    /// real edges to rewrite -- so the signature is deliberately fixer-less (explicit type
    /// arguments; accepted with eyes open). Returns node values unchanged over the
    /// same shape with each payload PAIRED (THE EDGE-PAIRING AMENDMENT --
    /// aggregation pairs, projection replaces): the original payload with the value the
    /// survey dispatched along the edge, as <see cref="DagEdgeResult{TEdge, TDispatch}"/>.
    /// Project <c>.SelectEdges(e =&gt; e.Edge.Accumulate)</c> when only the computed values
    /// should travel on.
    /// </summary>
    public static DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>> SourcefixDispatchEdges<TNode, TEdge, TDispatch>(
      this IDagnumerable<TNode, TEdge> source,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      return DispatchEdgesBuffer(source.Materialize(), DagFlowOrientation.Sourcefix, survey);
    }

    // The shared edge-writer core: payload slots are the exactly-once landing sites (each edge
    // is one non-boundary node's target exactly once, either orientation), and the cascade --
    // the far side's already-rewritten payloads -- assembles from the slots the flow direction
    // has already settled.
    internal static DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>> DispatchEdgesBuffer<TNode, TEdge, TDispatch>(
      DagBuffer<TNode, TEdge> buffer,
      DagFlowOrientation orientation,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      var structure = buffer.Structure;
      var count = buffer.Count;
      var sourcefix = orientation == DagFlowOrientation.Sourcefix;
      var (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      var dispatched = new TDispatch[structure.EdgeCount];

      for (var step = 0; step < count; step++)
      {
        var ordinal = sourcefix ? step : count - 1 - step;

        var targetFrom = sourcefix ? structure.OutOffsets[ordinal] : inOffsets[ordinal];
        var targetUpTo = sourcefix ? structure.OutOffsets[ordinal + 1] : inOffsets[ordinal + 1];
        if (targetFrom == targetUpTo)
          continue;

        // The cascade: the arrival adjacency's payloads, already rewritten by the far side.
        var arrivalFrom = sourcefix ? inOffsets[ordinal] : structure.OutOffsets[ordinal];
        var arrivalUpTo = sourcefix ? inOffsets[ordinal + 1] : structure.OutOffsets[ordinal + 1];

        IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>> nodeArrivals;
        if (arrivalFrom == arrivalUpTo)
        {
          nodeArrivals = Array.Empty<DagDispatchInflow<TNode, TDispatch, TEdge>>();
        }
        else
        {
          var arrived = new DagDispatchInflow<TNode, TDispatch, TEdge>[arrivalUpTo - arrivalFrom];
          for (var index = 0; index < arrived.Length; index++)
          {
            if (sourcefix)
            {
              var inSlot = arrivalFrom + index;
              arrived[index] = new DagDispatchInflow<TNode, TDispatch, TEdge>(
                buffer[inParents[inSlot]],
                dispatched[inEdgeOutSlots[inSlot]],
                structure.OutPayloads[inEdgeOutSlots[inSlot]]);
            }
            else
            {
              var outSlot = arrivalFrom + index;
              arrived[index] = new DagDispatchInflow<TNode, TDispatch, TEdge>(
                buffer[structure.OutTargets[outSlot]],
                dispatched[outSlot],
                structure.OutPayloads[outSlot]);
            }
          }
          nodeArrivals = arrived;
        }

        var targets = new DagDispatchTarget<TNode, TDispatch, TEdge>[targetUpTo - targetFrom];
        for (var index = 0; index < targets.Length; index++)
        {
          if (sourcefix)
          {
            var outSlot = targetFrom + index;
            targets[index] = new DagDispatchTarget<TNode, TDispatch, TEdge>(
              buffer[structure.OutTargets[outSlot]], structure.OutPayloads[outSlot], outSlot);
          }
          else
          {
            var inSlot = targetFrom + index;
            targets[index] = new DagDispatchTarget<TNode, TDispatch, TEdge>(
              buffer[inParents[inSlot]], structure.OutPayloads[inEdgeOutSlots[inSlot]], inEdgeOutSlots[inSlot]);
          }
        }

        survey(buffer[ordinal], nodeArrivals, targets);

        for (var index = 0; index < targets.Length; index++)
        {
          if (!targets[index].IsDispatched)
            throw new InvalidOperationException($"The edge to '{targets[index].Value}' was not dispatched.");
          dispatched[targets[index].Slot] = targets[index].DispatchedValue;
        }
      }

      // The pairing at the result boundary (the amendment's whole content): the machinery
      // held old payload and dispatched value together all pass long -- the arrival seat is
      // the proof -- so the buffer keeps them together too.
      var results = new DagEdgeResult<TEdge, TDispatch>[structure.EdgeCount];
      for (var slot = 0; slot < results.Length; slot++)
        results[slot] = new DagEdgeResult<TEdge, TDispatch>(structure.OutPayloads[slot], dispatched[slot]);

      return buffer.WithPayloads(results);
    }
  }
}
