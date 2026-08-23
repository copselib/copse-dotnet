using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Rewrite every node's OUT-EDGES from the node's own seat, given its whole event: the
    /// selector returns one payload per departure, in departure order. The dispatching end's
    /// projection -- equal to the bind of <c>Return</c> answering <c>Rewrite</c> for every
    /// out-edge (pinned), here at the extend's price and with the result payload type free.
    /// Every edge is rewritten exactly once, at its parent; shape and seats untouched.
    /// </summary>
    public static DagBuffer<TNode, TEdgeResult> SelectOutEdges<TNode, TEdge, TEdgeResult>(
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

      var payloads = new TEdgeResult[structure.EdgeCount];

      for (var ordinal = 0; ordinal < buffer.Count; ordinal++)
      {
        var rewritten = selector(arrivals[ordinal], buffer[ordinal], departures[ordinal]);

        DagSeats.RequireOnePerSeat(nameof(SelectOutEdges), ordinal, rewritten, departures[ordinal].Length, "payloads");

        for (var index = 0; index < rewritten.Count; index++)
          payloads[structure.OutOffsets[ordinal] + index] = rewritten[index];
      }

      return buffer.WithPayloads(payloads);
    }
  }
}
