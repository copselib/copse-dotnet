namespace Copse.Dags
{
  // The event-grain seats, built once per operator call over a buffer: for every node, its
  // ARRIVALS (in-edges as DagEdgeContext: parent value, this value, payload, in-edge index) and
  // its DEPARTURES (out-edges likewise, each carrying the in-edge index it has at its child).
  // The grouped event's two groups as plain arrays -- what the extend-shaped relabels
  // (SelectNodes over the event, SelectInEdges, SelectOutEdges, the group-aware prunes) read.
  internal static class DagEventSeats
  {
    public static void Build<TNode, TEdge>(
      DagBuffer<TNode, TEdge> buffer,
      out DagEdgeContext<TNode, TEdge>[][] arrivals,
      out DagEdgeContext<TNode, TEdge>[][] departures,
      out int[] inEdgeIndexOfOutSlot)
    {
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;
      var (inOffsets, inParents, inEdgeOutSlots) = structure.InAdjacency();

      inEdgeIndexOfOutSlot = new int[outTargets.Length];
      arrivals = new DagEdgeContext<TNode, TEdge>[nodeCount][];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        var group = new DagEdgeContext<TNode, TEdge>[inOffsets[ordinal + 1] - inOffsets[ordinal]];

        for (var index = 0; index < group.Length; index++)
        {
          var inSlot = inOffsets[ordinal] + index;
          inEdgeIndexOfOutSlot[inEdgeOutSlots[inSlot]] = index;
          group[index] = new DagEdgeContext<TNode, TEdge>(buffer[inParents[inSlot]], buffer[ordinal], outPayloads[inEdgeOutSlots[inSlot]], index);
        }

        arrivals[ordinal] = group;
      }

      departures = new DagEdgeContext<TNode, TEdge>[nodeCount][];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        var group = new DagEdgeContext<TNode, TEdge>[outOffsets[ordinal + 1] - outOffsets[ordinal]];

        for (var index = 0; index < group.Length; index++)
        {
          var slot = outOffsets[ordinal] + index;
          group[index] = new DagEdgeContext<TNode, TEdge>(buffer[ordinal], buffer[outTargets[slot]], outPayloads[slot], inEdgeIndexOfOutSlot[slot]);
        }

        departures[ordinal] = group;
      }
    }
  }
}
