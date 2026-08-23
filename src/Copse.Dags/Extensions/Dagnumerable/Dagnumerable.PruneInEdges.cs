using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Prune IN-EDGES from the node's own seat, given its whole event: the predicate answers
    /// one verdict per arrival, in arrival order (true = prune, the prune family's polarity).
    /// Liveness does the rest: a node whose every in-edge is pruned or dead dies, cascading,
    /// unless it was an original source. The transpose-conjugate of
    /// <see cref="PruneOutEdges"/> (pinned). Capture-shaped.
    /// </summary>
    public static DagBuffer<TNode, TEdge> PruneInEdges<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<IReadOnlyList<DagEdgeContext<TNode, TEdge>>, TNode, IReadOnlyList<DagEdgeContext<TNode, TEdge>>, IReadOnlyList<bool>> predicate)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      DagEventSeats.Build(buffer, out var arrivals, out var departures);
      var (inOffsets, _, inEdgeOutSlots) = structure.InAdjacency();

      var kept = new bool[structure.EdgeCount];
      for (var slot = 0; slot < kept.Length; slot++)
        kept[slot] = true;

      for (var ordinal = 0; ordinal < buffer.Count; ordinal++)
      {
        var verdicts = predicate(arrivals[ordinal], buffer[ordinal], departures[ordinal]);

        DagSeats.RequireOnePerSeat(nameof(PruneInEdges), ordinal, verdicts, arrivals[ordinal].Length, "verdicts");

        for (var index = 0; index < verdicts.Count; index++)
          if (verdicts[index])
            kept[inEdgeOutSlots[inOffsets[ordinal] + index]] = false;
      }

      return DagCompaction.KeepEdges(buffer, kept);
    }
  }
}
