using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Downward cumulative scan over the contract: <paramref name="accumulate"/> receives each
    /// node and one EDGE-PAIRED inflow per live in-edge -- the parent's accumulated result with
    /// the payload it arrived on, in discovery order; empty at sources, which is the call that
    /// seeds the scan (the fused-callback shape, A/B-ruled: kept; the four-seat
    /// dual fold is logged for the future). Runs the pass NOW -- a scan's value is an
    /// entry-time fact under multiple parentage, so a lazy dag of results cannot honestly
    /// exist (design-docs/DAG_CONTRACT_DESIGN.md, open question 7) -- and returns the CANONICAL
    /// PAIRING: a <see cref="DagBuffer{TNode, TEdge}"/> of
    /// <see cref="DagScanResult{TNode, TAccumulate}"/>s over the source's SHARED structure
    /// (project <c>.Accumulate</c> for values). Callbacks fire when their data is ready
    /// (arrivals complete); only the per-group order is contract -- the total cross-node
    /// order is deliberately unspecified.
    /// </summary>
    public static DagBuffer<DagScanResult<TNode, TResult>, TEdge> SourcefixScan<TNode, TResult, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
    {
      if (accumulate == null)
        throw new ArgumentNullException(nameof(accumulate));

      return ScanBuffer(source.Materialize(), DagFlowOrientation.Sourcefix, accumulate);
    }

    // The shared fold core: one pass over the buffer in flow order (sourcefix: entry order,
    // parents settle first; sinkfix: reverse entry order, children settle first), assembling
    // each node's edge-paired arrival group from the flat adjacency the orientation selects.
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
  }
}
