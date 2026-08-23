using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The flow family's engines, orientation-read: one fold core, two survey cores, each run
  // over a capture in the walk order the orientation names (sourcefix = the buffer's
  // topological order, sinkfix = its reverse, arrivals in out-edge order). DagFlow is the
  // surface; nothing else reaches these.
  internal static class DagFlowEngines
  {
    internal static DagBuffer<DagScanResult<TNode, TResult>, TEdge> ScanBuffer<TNode, TResult, TEdge>(
      DagBuffer<TNode, TEdge> buffer,
      DagFlowOrientation orientation,
      Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
    {
      var structure = buffer.Structure;
      var count = buffer.Count;
      var sourcefix = orientation == DagFlowOrientation.Sourcefix;
      int[] inOffsets = null, inParents = null, inEdgeOutSlots = null;
      if (sourcefix)
        (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      var results = new TResult[count];
      var pairs = new DagScanResult<TNode, TResult>[count];

      for (var step = 0; step < count; step++)
      {
        var ordinal = sourcefix ? step : count - 1 - step;

        int arrivalDegree;
        if (sourcefix)
          arrivalDegree = inOffsets[ordinal + 1] - inOffsets[ordinal];
        else
          arrivalDegree = structure.OutOffsets[ordinal + 1] - structure.OutOffsets[ordinal];

        IReadOnlyList<DagInflow<TResult, TEdge>> inflows;
        if (arrivalDegree == 0)
        {
          inflows = Array.Empty<DagInflow<TResult, TEdge>>();
        }
        else
        {
          var arrived = new DagInflow<TResult, TEdge>[arrivalDegree];
          for (var index = 0; index < arrivalDegree; index++)
          {
            if (sourcefix)
            {
              var inSlot = inOffsets[ordinal] + index;
              arrived[index] = new DagInflow<TResult, TEdge>(
                results[inParents[inSlot]], structure.OutPayloads[inEdgeOutSlots[inSlot]]);
            }
            else
            {
              var outSlot = structure.OutOffsets[ordinal] + index;
              arrived[index] = new DagInflow<TResult, TEdge>(
                results[structure.OutTargets[outSlot]], structure.OutPayloads[outSlot]);
            }
          }
          inflows = arrived;
        }

        results[ordinal] = accumulate(buffer[ordinal], inflows);
        pairs[ordinal] = new DagScanResult<TNode, TResult>(buffer[ordinal], results[ordinal]);
      }

      return buffer.WithValues(pairs);
    }

    internal static DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> DispatchBuffer<TNode, TDispatch, TEdge>(
      DagBuffer<TNode, TEdge> buffer,
      TDispatch seed,
      DagFlowOrientation orientation,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      var structure = buffer.Structure;
      var count = buffer.Count;
      var sourcefix = orientation == DagFlowOrientation.Sourcefix;
      var (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      // Arrival layout: one slot per arrival edge (in-edges downward, out-edges upward), plus
      // one virtual slot per boundary node when sourcefix.
      var arrivalOffsets = new int[count + 1];
      for (var ordinal = 0; ordinal < count; ordinal++)
      {
        var arrivalDegree = sourcefix
          ? inOffsets[ordinal + 1] - inOffsets[ordinal]
          : structure.OutOffsets[ordinal + 1] - structure.OutOffsets[ordinal];
        if (arrivalDegree == 0 && sourcefix)
          arrivalDegree = 1;
        arrivalOffsets[ordinal + 1] = arrivalOffsets[ordinal] + arrivalDegree;
      }

      var arrivals = new TDispatch[arrivalOffsets[count]];

      // A target edge's landing slot in the far node's arrival block. Downward, out-slot s
      // lands at its in-edge index within the child's block; upward, in-slot j lands at the
      // edge's out-edge index within the parent's block -- direct arithmetic either way.
      int[] arrivalSlotOfOutSlot = null;
      if (sourcefix)
      {
        arrivalSlotOfOutSlot = new int[structure.EdgeCount];
        for (var ordinal = 0; ordinal < count; ordinal++)
          for (var inSlot = inOffsets[ordinal]; inSlot < inOffsets[ordinal + 1]; inSlot++)
            arrivalSlotOfOutSlot[inEdgeOutSlots[inSlot]] = arrivalOffsets[ordinal] + (inSlot - inOffsets[ordinal]);
      }

      static void Land(
        IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>> targets, TDispatch[] arrivalSlots)
      {
        for (var index = 0; index < targets.Count; index++)
        {
          if (!targets[index].IsDispatched)
            throw new InvalidOperationException($"The edge to '{targets[index].Value}' was not dispatched.");
          arrivalSlots[targets[index].Slot] = targets[index].DispatchedValue;
        }
      }

      // The virtual source family: the boundary is an invocation of the same survey.
      if (sourcefix)
      {
        var boundaryTargets = new List<DagDispatchTarget<TNode, TDispatch, TEdge>>();
        for (var ordinal = 0; ordinal < count; ordinal++)
          if (inOffsets[ordinal + 1] == inOffsets[ordinal])
            boundaryTargets.Add(new DagDispatchTarget<TNode, TDispatch, TEdge>(
              buffer[ordinal], default, arrivalOffsets[ordinal]));

        var seedArrival = new[] { new DagDispatchInflow<TNode, TDispatch, TEdge>(default, seed, default) };
        survey(default, seedArrival, boundaryTargets);
        Land(boundaryTargets, arrivals);
      }

      for (var step = 0; step < count; step++)
      {
        var ordinal = sourcefix ? step : count - 1 - step;

        var targetFrom = sourcefix ? structure.OutOffsets[ordinal] : inOffsets[ordinal];
        var targetUpTo = sourcefix ? structure.OutOffsets[ordinal + 1] : inOffsets[ordinal + 1];
        if (targetFrom == targetUpTo)
          continue;

        var arrivalCount = arrivalOffsets[ordinal + 1] - arrivalOffsets[ordinal];
        IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>> nodeArrivals;
        if (arrivalCount == 0)
        {
          nodeArrivals = Array.Empty<DagDispatchInflow<TNode, TDispatch, TEdge>>();
        }
        else if (sourcefix && inOffsets[ordinal + 1] == inOffsets[ordinal])
        {
          // A source: its single arrival is the virtual family's delivery, dispatcher-less.
          nodeArrivals = new[]
          {
            new DagDispatchInflow<TNode, TDispatch, TEdge>(default, arrivals[arrivalOffsets[ordinal]], default),
          };
        }
        else
        {
          var arrived = new DagDispatchInflow<TNode, TDispatch, TEdge>[arrivalCount];
          for (var index = 0; index < arrivalCount; index++)
          {
            if (sourcefix)
            {
              var inSlot = inOffsets[ordinal] + index;
              arrived[index] = new DagDispatchInflow<TNode, TDispatch, TEdge>(
                buffer[inParents[inSlot]],
                arrivals[arrivalOffsets[ordinal] + index],
                structure.OutPayloads[inEdgeOutSlots[inSlot]]);
            }
            else
            {
              var outSlot = structure.OutOffsets[ordinal] + index;
              arrived[index] = new DagDispatchInflow<TNode, TDispatch, TEdge>(
                buffer[structure.OutTargets[outSlot]],
                arrivals[arrivalOffsets[ordinal] + index],
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
              buffer[structure.OutTargets[outSlot]],
              structure.OutPayloads[outSlot],
              arrivalSlotOfOutSlot[outSlot]);
          }
          else
          {
            var inSlot = targetFrom + index;
            var parent = inParents[inSlot];
            var outSlot = inEdgeOutSlots[inSlot];
            targets[index] = new DagDispatchTarget<TNode, TDispatch, TEdge>(
              buffer[parent],
              structure.OutPayloads[outSlot],
              arrivalOffsets[parent] + (outSlot - structure.OutOffsets[parent]));
          }
        }

        survey(buffer[ordinal], nodeArrivals, targets);
        Land(targets, arrivals);
      }

      var pairs = new DagDispatchResult<TNode, TDispatch>[count];
      for (var ordinal = 0; ordinal < count; ordinal++)
        pairs[ordinal] = new DagDispatchResult<TNode, TDispatch>(
          buffer[ordinal],
          new DagArrivals<TDispatch>(arrivals, arrivalOffsets[ordinal], arrivalOffsets[ordinal + 1] - arrivalOffsets[ordinal]));

      return buffer.WithValues(pairs);
    }

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

      // The pairing at the result boundary: the machinery
      // held old payload and dispatched value together all pass long -- the arrival seat is
      // the proof -- so the buffer keeps them together too.
      var results = new DagEdgeResult<TEdge, TDispatch>[structure.EdgeCount];
      for (var slot = 0; slot < results.Length; slot++)
        results[slot] = new DagEdgeResult<TEdge, TDispatch>(structure.OutPayloads[slot], dispatched[slot]);

      return buffer.WithPayloads(results);
    }
  }
}
