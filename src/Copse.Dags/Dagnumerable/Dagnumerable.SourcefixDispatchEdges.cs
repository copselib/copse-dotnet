using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-result flavor of the downward survey pass -- <see cref="SinkfixDispatchEdges"/>'s
    /// mirror (docs/DAG_CONTRACT_DESIGN.md, the edge dual, tier 2): what each survey dispatches
    /// BECOMES the result's edge payloads. Every node with out-edges is surveyed once, in
    /// readiness order, with the destructured seats (2026-08-05): its value, its IN-edges'
    /// already-rewritten payloads as edge-paired arrivals (ancestors' cascade; empty at
    /// sources; the old payload rides as the arrival's Edge, the dispatching parent as its
    /// Dispatcher), and one exactly-once <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/>
    /// per OUT-edge (child value, old payload; out-edge order). Sinks are never surveyed, yet
    /// every edge is written exactly once: each edge is precisely one non-sink node's
    /// out-edge. The natural home of path-cumulative edge values; the upward twin owns in-edge
    /// group algebra. There is NO boundary invocation and no seed -- a virtual family has no
    /// real edges to rewrite -- so the signature is deliberately fixer-less (explicit type
    /// arguments; accepted with eyes open, 2026-08-05). Returns node values unchanged over the
    /// same shape with payloads replaced.
    /// </summary>
    public static DagBuffer<TNode, TEdgeResult> SourcefixDispatchEdges<TNode, TEdge, TEdgeResult>(
      this IDagnumerable<TNode, TEdge> source,
      DagDispatchSurvey<TNode, TEdgeResult, TEdge> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      return DispatchEdgesBuffer(source.Materialize(), DagFlowOrientation.Sourcefix, survey);
    }

    // The shared edge-writer core: payload slots are the exactly-once landing sites (each edge
    // is one non-boundary node's target exactly once, either orientation), and the cascade --
    // the far side's already-rewritten payloads -- assembles from the slots the flow direction
    // has already settled.
    internal static DagBuffer<TNode, TEdgeResult> DispatchEdgesBuffer<TNode, TEdge, TEdgeResult>(
      DagBuffer<TNode, TEdge> buffer,
      DagFlowOrientation orientation,
      DagDispatchSurvey<TNode, TEdgeResult, TEdge> survey)
    {
      var structure = buffer.Structure;
      var count = buffer.Count;
      var sourcefix = orientation == DagFlowOrientation.Sourcefix;
      var (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      var newPayloads = new TEdgeResult[structure.EdgeCount];

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

        IReadOnlyList<DagDispatchInflow<TNode, TEdgeResult, TEdge>> nodeArrivals;
        if (arrivalFrom == arrivalUpTo)
        {
          nodeArrivals = Array.Empty<DagDispatchInflow<TNode, TEdgeResult, TEdge>>();
        }
        else
        {
          var arrived = new DagDispatchInflow<TNode, TEdgeResult, TEdge>[arrivalUpTo - arrivalFrom];
          for (var index = 0; index < arrived.Length; index++)
          {
            if (sourcefix)
            {
              var inSlot = arrivalFrom + index;
              arrived[index] = new DagDispatchInflow<TNode, TEdgeResult, TEdge>(
                buffer[inParents[inSlot]],
                newPayloads[inEdgeOutSlots[inSlot]],
                structure.OutPayloads[inEdgeOutSlots[inSlot]]);
            }
            else
            {
              var outSlot = arrivalFrom + index;
              arrived[index] = new DagDispatchInflow<TNode, TEdgeResult, TEdge>(
                buffer[structure.OutTargets[outSlot]],
                newPayloads[outSlot],
                structure.OutPayloads[outSlot]);
            }
          }
          nodeArrivals = arrived;
        }

        var targets = new DagDispatchTarget<TNode, TEdgeResult, TEdge>[targetUpTo - targetFrom];
        for (var index = 0; index < targets.Length; index++)
        {
          if (sourcefix)
          {
            var outSlot = targetFrom + index;
            targets[index] = new DagDispatchTarget<TNode, TEdgeResult, TEdge>(
              buffer[structure.OutTargets[outSlot]], structure.OutPayloads[outSlot], outSlot);
          }
          else
          {
            var inSlot = targetFrom + index;
            targets[index] = new DagDispatchTarget<TNode, TEdgeResult, TEdge>(
              buffer[inParents[inSlot]], structure.OutPayloads[inEdgeOutSlots[inSlot]], inEdgeOutSlots[inSlot]);
          }
        }

        survey(buffer[ordinal], nodeArrivals, targets);

        for (var index = 0; index < targets.Length; index++)
        {
          if (!targets[index].IsDispatched)
            throw new InvalidOperationException($"The edge to '{targets[index].Value}' was not dispatched.");
          newPayloads[targets[index].Slot] = targets[index].DispatchedValue;
        }
      }

      return buffer.WithPayloads(newPayloads);
    }
  }
}
