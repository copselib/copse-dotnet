using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Prune OUT-EDGES from the node's own seat, given its whole event: the predicate answers
    /// one verdict per departure, in departure order (true = prune). Equal to the bind of
    /// <c>Return</c> answering <c>Suppress</c> for the pruned out-edges (pinned); liveness
    /// settles the children. Capture-shaped.
    /// </summary>
    public static DagBuffer<TNode, TEdge> PruneOutEdges<TNode, TEdge>(
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

      var kept = new bool[structure.EdgeCount];

      for (var ordinal = 0; ordinal < buffer.Count; ordinal++)
      {
        var verdicts = predicate(arrivals[ordinal], buffer[ordinal], departures[ordinal]);

        DagSeats.RequireOnePerSeat(nameof(PruneOutEdges), ordinal, verdicts, departures[ordinal].Length, "verdicts");

        for (var index = 0; index < verdicts.Count; index++)
          kept[structure.OutOffsets[ordinal] + index] = !verdicts[index];
      }

      return DagCompaction.KeepEdges(buffer, kept);
    }
  }
}
