using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Rewrite every node's IN-EDGES from the node's own seat, given its whole event: the
    /// selector returns one payload per arrival, in arrival order. The group-aware edge
    /// projection: conditioning an owner group -- reallocating an excluded owner's fraction over
    /// the survivors -- is this, with no value flowing, no slots, no pairing to unwrap. An
    /// extend, not a bind: the child reads its
    /// group and relabels the edges arriving at it; equal to the transpose-conjugate of
    /// <see cref="SelectOutEdges"/> (pinned). Every edge is rewritten exactly once, at its
    /// child; shape and seats untouched.
    /// </summary>
    public static DagBuffer<TNode, TEdgeResult> SelectInEdges<TNode, TEdge, TEdgeResult>(
      this IDagnumerable<TNode, TEdge> source,
      Func<IReadOnlyList<DagEdgeContext<TNode, TEdge>>, TNode, IReadOnlyList<DagEdgeContext<TNode, TEdge>>, IReadOnlyList<TEdgeResult>> selector)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      DagEventSeats.Build(buffer, out var arrivals, out var departures);
      var (inOffsets, _, inEdgeOutSlots) = structure.InAdjacency();

      var payloads = new TEdgeResult[structure.EdgeCount];

      for (var ordinal = 0; ordinal < buffer.Count; ordinal++)
      {
        var rewritten = selector(arrivals[ordinal], buffer[ordinal], departures[ordinal]);

        DagSeats.RequireOnePerSeat(nameof(SelectInEdges), ordinal, rewritten, arrivals[ordinal].Length, "payloads");

        for (var index = 0; index < rewritten.Count; index++)
          payloads[inEdgeOutSlots[inOffsets[ordinal] + index]] = rewritten[index];
      }

      return buffer.WithPayloads(payloads);
    }
  }
}
