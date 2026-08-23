using System.Collections.Generic;

namespace Copse.Dags
{
  // Keep a subset of a buffer's edges and let liveness settle the nodes: a node survives as an
  // original source or through a kept edge from a surviving parent (topological order settles
  // every parent first); a dead node's edges are gone with it. Seats carry. The shared
  // machinery of the group-aware prunes (PruneInEdges, PruneOutEdges).
  internal static class DagEdgeCompaction
  {
    public static DagBuffer<TNode, TEdge> KeepEdges<TNode, TEdge>(DagBuffer<TNode, TEdge> buffer, bool[] keptSlots)
    {
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      var inDegrees = new int[nodeCount];
      for (var slot = 0; slot < outTargets.Length; slot++)
        inDegrees[outTargets[slot]]++;

      var live = new bool[nodeCount];
      var hasLiveInbound = new bool[nodeCount];
      var denseOf = new int[nodeCount];
      var values = new List<TNode>();
      var sourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        live[ordinal] = inDegrees[ordinal] == 0 || hasLiveInbound[ordinal];
        denseOf[ordinal] = -1;

        if (!live[ordinal])
          continue;

        denseOf[ordinal] = values.Count;
        values.Add(buffer[ordinal]);
        sourceOrdinals.Add(buffer.SourceOrdinal(ordinal));

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          if (keptSlots[slot])
            hasLiveInbound[outTargets[slot]] = true;
      }

      var offsets = new List<int>(values.Count + 1) { 0 };
      var targets = new List<int>();
      var payloads = new List<TEdge>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!live[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (!keptSlots[slot])
            continue;

          targets.Add(denseOf[outTargets[slot]]);
          payloads.Add(outPayloads[slot]);
        }

        offsets.Add(targets.Count);
      }

      return DagBuffer<TNode, TEdge>.FromParts(
        values.ToArray(),
        new DagStructure<TEdge>(offsets.ToArray(), targets.ToArray(), payloads.ToArray()),
        sourceOrdinals.ToArray());
    }
  }
}
