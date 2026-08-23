using System.Collections.Generic;

namespace Copse.Dags
{
  // The family's one capture-to-capture compaction: keep the live nodes (dense ordinals ARE a
  // topological order, so their relative order is a topological order of the induced sub-dag)
  // and, among them, the kept slots -- re-keyed to dense result ordinals, seats carried.
  internal static class DagCompaction
  {
    /// <summary>
    /// Drops every slot whose <paramref name="keptSlots"/> entry is false, then every node that
    /// loses its last inbound path (original sources always live).
    /// </summary>
    public static DagBuffer<TNode, TEdge> KeepEdges<TNode, TEdge>(DagBuffer<TNode, TEdge> buffer, bool[] keptSlots)
    {
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var inDegrees = structure.InDegrees();

      var live = new bool[nodeCount];
      var hasLiveInbound = new bool[nodeCount];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        live[ordinal] = inDegrees[ordinal] == 0 || hasLiveInbound[ordinal];

        if (!live[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          if (keptSlots[slot])
            hasLiveInbound[outTargets[slot]] = true;
      }

      return Compact(buffer, live, keptSlots);
    }

    /// <summary>
    /// The sub-dag induced by <paramref name="liveNodes"/>, restricted to the kept slots
    /// (<c>null</c> keeps every edge between live nodes). The buffer itself when nothing goes.
    /// </summary>
    public static DagBuffer<TNode, TEdge> Compact<TNode, TEdge>(DagBuffer<TNode, TEdge> buffer, bool[] liveNodes, bool[] keptSlots)
    {
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      var denseOf = new int[nodeCount];
      var values = new List<TNode>();
      var sourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        denseOf[ordinal] = liveNodes[ordinal] ? values.Count : -1;

        if (!liveNodes[ordinal])
          continue;

        values.Add(buffer[ordinal]);
        sourceOrdinals.Add(buffer.SourceOrdinal(ordinal));
      }

      if (values.Count == nodeCount && keptSlots == null)
        return buffer;

      var offsets = new List<int>(values.Count + 1) { 0 };
      var targets = new List<int>();
      var payloads = new List<TEdge>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!liveNodes[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (!liveNodes[outTargets[slot]] || (keptSlots != null && !keptSlots[slot]))
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
