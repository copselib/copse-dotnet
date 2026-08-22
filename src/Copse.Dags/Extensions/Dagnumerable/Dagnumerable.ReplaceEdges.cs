using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// EDGE REPLACEMENT (design-docs/DAG_CONTRACT_DESIGN.md -- graph rewriting's own
    /// term; the bind restricted to edge elements -- <c>Subdivide().SelectMany(path as a
    /// chain-shaped expansion).Unsubdivide()</c>, pinned content-exact -- kept as the native
    /// fast path): every edge becomes the
    /// <see cref="DagEdgePath{TNode, TEdge}"/> the selector returns -- kept, rewritten,
    /// subdivided through fresh interior nodes, or dropped. The node channel is untouched
    /// except as paths dictate: interior nodes are BORN here (their
    /// <see cref="DagBuffer{TNode, TEdge}.SourceOrdinal"/> is −1, the born-here marker), and
    /// a node that loses its last inbound path dies by the family's LIVENESS rule -- exactly
    /// <c>PruneEdges</c>' semantics, which is the replacement's streaming special case
    /// (all-<see cref="DagEdgePath{TNode, TEdge}.Drop"/>-or-<c>Keep</c>), as
    /// <c>SelectEdges</c> is its streaming pure-rewrite special case (all-<c>Keep</c>). Both
    /// keep their seats: different cost classes, not aliases.
    ///
    /// <para>The selector runs once per live edge (an edge whose parent died is never
    /// consulted), in topological parent-major order, out-edges in order -- deterministic;
    /// purity expected, the house contract. Interior nodes are always fresh (no value
    /// comparison, so no existing node can be referenced), which makes the replacement CYCLE-SAFE BY
    /// CONSTRUCTION: the result buffer inherits its source's acyclicity certificate without
    /// revalidation.</para>
    ///
    /// <para>Returns a buffer BY CONVENTION, not theorem (the lazy-builder distinction):
    /// streaming subdivision is visit-protocol-legal, but synthesized nodes need ordinals a
    /// wrapper cannot know are free -- the reserved-range amendment, logged for its sitting,
    /// would make this streamable. Until then: capture in, capture out, one pass each way.</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> ReplaceEdges<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<DagEdgeContext<TNode, TEdge>, DagEdgePath<TNode, TEdge>> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      // Each slot's index among its child's in-edges, in discovery order -- the context's
      // correlation key, by the same arithmetic the relationship tracker streams.
      var (inOffsets, _, inEdgeOutSlots) = structure.InAdjacency();
      var slotInEdgeIndex = new int[structure.EdgeCount];
      for (var child = 0; child < nodeCount; child++)
        for (var inSlot = inOffsets[child]; inSlot < inOffsets[child + 1]; inSlot++)
          slotInEdgeIndex[inEdgeOutSlots[inSlot]] = inSlot - inOffsets[child];

      // The liveness sweep: dense ordinals ARE a topological order, so every parent's fate is
      // settled before its children are reached. A node survives as an original source or by
      // keeping at least one non-dropped inbound path from a surviving parent; dead nodes'
      // edges are never consulted.
      var survives = new bool[nodeCount];
      var hasLiveInboundPath = new bool[nodeCount];
      var paths = new DagEdgePath<TNode, TEdge>[structure.EdgeCount];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        survives[ordinal] = hasLiveInboundPath[ordinal] || (inOffsets[ordinal] == inOffsets[ordinal + 1]);

        if (!survives[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          paths[slot] = selector(new DagEdgeContext<TNode, TEdge>(
            buffer[ordinal], buffer[outTargets[slot]], outPayloads[slot], slotInEdgeIndex[slot]));

          if (!paths[slot].IsDrop)
            hasLiveInboundPath[outTargets[slot]] = true;
        }
      }

      // Pass A -- placement: survivors keep their relative order; a path's interior nodes sit
      // immediately after their parent (before every later original, so before the path's
      // target), preserving topological entry order by construction.
      var resultOrdinalOf = new int[nodeCount];
      var chainOrdinals = new int[structure.EdgeCount][];
      var resultValues = new List<TNode>();
      var resultSourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!survives[ordinal])
        {
          resultOrdinalOf[ordinal] = -1;
          continue;
        }

        resultOrdinalOf[ordinal] = resultValues.Count;
        resultValues.Add(buffer[ordinal]);
        resultSourceOrdinals.Add(buffer.SourceOrdinal(ordinal));

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          var links = paths[slot].IsDrop ? null : paths[slot].Links;

          if (links == null || links.Count == 0)
            continue;

          chainOrdinals[slot] = new int[links.Count];
          for (var linkIndex = 0; linkIndex < links.Count; linkIndex++)
          {
            chainOrdinals[slot][linkIndex] = resultValues.Count;
            resultValues.Add(links[linkIndex].Node);
            resultSourceOrdinals.Add(-1); // born here
          }
        }
      }

      // Pass B -- the edge blocks, in result-ordinal order (the same order pass A assigned):
      // each survivor's transformed block, then its paths' single-edge interior blocks.
      var resultOffsets = new List<int>(resultValues.Count + 1) { 0 };
      var resultTargets = new List<int>();
      var resultPayloads = new List<TEdge>();

      void CloseBlock() => resultOffsets.Add(resultTargets.Count);

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!survives[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (paths[slot].IsDrop)
            continue;

          var links = paths[slot].Links;
          resultTargets.Add(links.Count > 0 ? chainOrdinals[slot][0] : resultOrdinalOf[outTargets[slot]]);
          resultPayloads.Add(paths[slot].FirstEdge);
        }

        CloseBlock();

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (paths[slot].IsDrop)
            continue;

          var links = paths[slot].Links;
          for (var linkIndex = 0; linkIndex < links.Count; linkIndex++)
          {
            resultTargets.Add(
              linkIndex + 1 < links.Count ? chainOrdinals[slot][linkIndex + 1] : resultOrdinalOf[outTargets[slot]]);
            resultPayloads.Add(links[linkIndex].Edge);
            CloseBlock();
          }
        }
      }

      return DagBuffer<TNode, TEdge>.FromParts(
        resultValues.ToArray(),
        new DagStructure<TEdge>(resultOffsets.ToArray(), resultTargets.ToArray(), resultPayloads.ToArray()),
        resultSourceOrdinals.ToArray());
    }
  }
}
