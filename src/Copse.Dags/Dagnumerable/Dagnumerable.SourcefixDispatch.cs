using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The survey-shaped downward pass (the work API's shape: a setter-callback allocator
    /// plugs in verbatim), re-founded on FULL PARTICIPATION (2026-08-05, matching the tree
    /// family): the boundary is an INVOCATION, not a callback -- the same
    /// <paramref name="survey"/> fires FIRST for the virtual source family
    /// (<c>default</c> subject, the seed as its single dispatcher-less arrival, the sources as
    /// targets), so every source's arrival is AUTHORED with the source in hand and a budget
    /// allocates ACROSS co-investing sources with the same callback that allocates everywhere
    /// else -- then once per node with live out-edges, in readiness order (arrivals complete
    /// before the survey; total cross-node order deliberately unspecified). Every target must
    /// be written exactly once (unwritten and double-written both throw). Returns the SURVEY
    /// TIER's pairing: a <see cref="DagBuffer{TNode, TEdge}"/> of
    /// <see cref="DagDispatchResult{TNode, TDispatch}"/>s -- each node with its complete
    /// arrival group in in-edge order (the recording rule: a survey records its INPUT), over
    /// the source's shared structure. Only live edges are surveyed, so pruning blockers
    /// upstream composes into the allocation for free.
    /// </summary>
    public static DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> SourcefixDispatch<TNode, TDispatch, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      TDispatch seed,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      return DispatchBuffer(source.Materialize(), seeded: true, seed, DagFlowOrientation.Sourcefix, survey);
    }

    // The shared survey core, orientation-parameterized (sinkfix is sourcefix-of-the-
    // transpose, read without materializing it): arrivals ride a flat slot array laid out per
    // node in the arrival adjacency's per-group order; targets land their writes into the far
    // node's slots; surveys fire in flow order, so a node's arrivals are complete when its
    // family is surveyed. Sinkfix runs unseeded (upward flow's boundary values live in the
    // nodes; sinks see empty arrivals) and never invokes a virtual family.
    internal static DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> DispatchBuffer<TNode, TDispatch, TEdge>(
      DagBuffer<TNode, TEdge> buffer,
      bool seeded,
      TDispatch seed,
      DagFlowOrientation orientation,
      DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      var structure = buffer.Structure;
      var count = buffer.Count;
      var sourcefix = orientation == DagFlowOrientation.Sourcefix;
      var (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      // Arrival layout: one slot per arrival edge (in-edges downward, out-edges upward), plus
      // one virtual slot per boundary node when seeded.
      var arrivalOffsets = new int[count + 1];
      for (var ordinal = 0; ordinal < count; ordinal++)
      {
        var arrivalDegree = sourcefix
          ? inOffsets[ordinal + 1] - inOffsets[ordinal]
          : structure.OutOffsets[ordinal + 1] - structure.OutOffsets[ordinal];
        if (arrivalDegree == 0 && seeded)
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
      if (seeded)
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
  }
}
