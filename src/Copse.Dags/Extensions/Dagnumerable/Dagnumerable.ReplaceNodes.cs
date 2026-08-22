using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// NODE REPLACEMENT (design-docs/SUBSTITUTION_TAXONOMY.md -- the node-channel row of
    /// the substitution taxonomy; the BROADCAST citizen beside the bind -- out-edges fan from
    /// EVERY replacement node, where <c>SelectMany</c> re-attaches them at a slot; lawful, pinned
    /// by DagNodeSubstitutionLawTests, and not derivable from the bind): every node becomes the
    /// <see cref="DagNodeGraph{TNode, TEdge}"/> the selector returns -- kept in its seat,
    /// split into alternatives, stretched into a chain, expanded into a graph, or dropped.
    ///
    /// <para>THE WIRING RULE (the lawful multiplicative pair -- every division copy keeps its
    /// own edge to each neighbor, the locality that makes deletion compose): the original's
    /// in-edges fan to the replacement's SOURCES; its out-edges fan from EVERY replacement
    /// node to the child replacement's sources. Payloads duplicate across a fan -- payload
    /// algebra over constrained groups stays the caller's business, the <c>PruneEdges</c>
    /// caveat's posture. <c>Drop</c> follows the family's one liveness rule (a node losing
    /// its last inbound path dies unless it was an original source), so <c>PruneNodesBefore</c> is
    /// this operator's all-<c>Keep</c>-or-<c>Drop</c> special case, as <c>SelectNodes</c> is its
    /// all-seat-keeping special case -- different cost classes, not aliases.</para>
    ///
    /// <para>The selector runs once per LIVE node (a dead node's replacement is never
    /// consulted -- the <c>ReplaceEdges</c> pin), in topological order -- deterministic;
    /// purity expected, the house contract. Multi-node replacements are wholly FRESH
    /// (born-here <c>SourceOrdinal</c> −1); the seat-keeping <see cref="DagNodeGraph{TNode,
    /// TEdge}.Keep"/> carries the original's. Internal edges run forward and replacements sit
    /// contiguously in their original's seat, so the result is topologically ordered and
    /// CYCLE-SAFE BY CONSTRUCTION -- the certificate transfers without revalidation.</para>
    ///
    /// <para>Returns a buffer BY CONVENTION, not theorem (the ledger in the taxonomy doc):
    /// synthesized nodes need ordinals a wrapper cannot know are free -- the same
    /// reserved-range amendment that would make <c>ReplaceEdges</c> streamable covers this
    /// operator too. Until then: capture in, capture out, one pass each way. (The seat rule
    /// is factory-based: only <c>Keep</c> carries a <c>SourceOrdinal</c>; every other shape
    /// is born-here regardless of node count.)</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> ReplaceNodes<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, DagNodeGraph<TNode, TEdge>> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      // Only "is this an original source" is needed, so a counting pass beats materializing
      // the full in-adjacency (which ReplaceEdges genuinely needs and this operator doesn't).
      var inDegrees = new int[nodeCount];
      for (var slot = 0; slot < outTargets.Length; slot++)
        inDegrees[outTargets[slot]]++;

      // The liveness sweep: dense ordinals ARE a topological order, so every parent's fate is
      // settled before its children are reached. A node survives as an original source or by
      // keeping at least one inbound edge from a surviving, non-dropped parent; dead and
      // dropped nodes feed nobody, and a dead node's selector is never consulted.
      var hasLiveInbound = new bool[nodeCount];
      var present = new bool[nodeCount];
      var replacements = new DagNodeGraph<TNode, TEdge>[nodeCount];
      var sourceIndicesOf = new int[nodeCount][];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        var survives = hasLiveInbound[ordinal] || inDegrees[ordinal] == 0;

        if (!survives)
          continue;

        replacements[ordinal] = selector(buffer[ordinal]);

        if (replacements[ordinal].IsDrop)
          continue;

        present[ordinal] = true;
        sourceIndicesOf[ordinal] = replacements[ordinal].SourceIndices();

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          hasLiveInbound[outTargets[slot]] = true;
      }

      // Pass A -- placement: each present original's replacement nodes sit contiguously in its
      // seat, in fragment order. In-edges arrive from earlier originals, internal edges run
      // forward, out-edges leave to later originals -- topological entry order by construction.
      var replacementStart = new int[nodeCount];
      var resultValues = new List<TNode>();
      var resultSourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!present[ordinal])
        {
          replacementStart[ordinal] = -1;
          continue;
        }

        replacementStart[ordinal] = resultValues.Count;
        var replacement = replacements[ordinal];
        var values = replacement.ValuesArray;
        var sourceOrdinal = replacement.KeepsSeat ? buffer.SourceOrdinal(ordinal) : -1;

        for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        {
          resultValues.Add(values[nodeIndex]);
          resultSourceOrdinals.Add(sourceOrdinal);
        }
      }

      // Pass B -- the out-blocks, in result-ordinal order: each replacement node's internal
      // edges first (declaration order -- own children before inherited, the taxonomy's
      // after-own-children convention), then the original's surviving out-edges fanned -- each
      // in out-edge order, to every source of the child's replacement in source order.
      var resultOffsets = new List<int>(resultValues.Count + 1) { 0 };
      var resultTargets = new List<int>();
      var resultPayloads = new List<TEdge>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!present[ordinal])
          continue;

        var replacement = replacements[ordinal];
        var values = replacement.ValuesArray;
        var edges = replacement.EdgesArray;

        for (var nodeIndex = 0; nodeIndex < values.Length; nodeIndex++)
        {
          // Replacements are authored values, so the per-node rescan is O(k·e) at
          // hand-written scale -- group-by-From the day a generator feeds this.
          if (edges != null)
            for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
              if (edges[edgeIndex].From != nodeIndex)
                continue;

              resultTargets.Add(replacementStart[ordinal] + edges[edgeIndex].To);
              resultPayloads.Add(edges[edgeIndex].Edge);
            }

          for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          {
            var child = outTargets[slot];

            if (!present[child])
              continue;

            foreach (var childSource in sourceIndicesOf[child])
            {
              resultTargets.Add(replacementStart[child] + childSource);
              resultPayloads.Add(outPayloads[slot]);
            }
          }

          resultOffsets.Add(resultTargets.Count);
        }
      }

      return DagBuffer<TNode, TEdge>.FromParts(
        resultValues.ToArray(),
        new DagStructure<TEdge>(resultOffsets.ToArray(), resultTargets.ToArray(), resultPayloads.ToArray()),
        resultSourceOrdinals.ToArray());
    }
  }
}
