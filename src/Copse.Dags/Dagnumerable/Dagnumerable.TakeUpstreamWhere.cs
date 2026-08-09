using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Selects the sub-dag upstream of the matching nodes: every match, everything that
    /// REACHES a match, and the edges among them -- one result dag, the matches its outlets
    /// (<see cref="TakeDownstreamWhere{TNode, TEdge}"/>' flow-reversed mirror; ratified
    /// 2026-08-09, the closure-question sitting -- the viewer's per-sink structures made the
    /// transpose sandwich the hot path). The mirror of the downstream emergence holds: a match
    /// that reaches another match keeps an out-edge inside the closure, so it comes out an
    /// interior node; the result's SINKS are exactly the matches that reach no further match.
    /// Shared ancestors are shared, never duplicated. Edges to OUTSIDE the closure die with
    /// their excluded children; because inclusion is an upward closure, every in-edge of an
    /// included node survives whole.
    ///
    /// <para>Semantically <c>Transpose().TakeDownstreamWhere(p).Transpose()</c> -- the law is
    /// pinned in the battery -- but implemented directly: one REVERSE sweep over the same
    /// out-CSR (dense ordinals are a topological order, so every child settles before its
    /// parent is reached; a node is included iff it matches or any out-target is included).
    /// No transpose, no in-adjacency, no intermediate buffers. Per-match separate closures
    /// are the caller's loop; the between-graph -- every path from x down to a sink -- is the
    /// composition <c>TakeDownstreamWhere(x).TakeUpstreamWhere(sink)</c>. The predicate is
    /// evaluated at most once per node, and not at all on nodes whose reach already includes a
    /// match (counts unspecified; purity expected).</para>
    ///
    /// <para>Returns a <see cref="DagBuffer{TNode, TEdge}"/> -- capture-shaped BY CONTRACT,
    /// the cluster's reasoning verbatim: the result's sources (the original sources that
    /// reach a match) are unknowable until the mark completes, so a lazy wrapper cannot
    /// honestly present them. One reverse pass marks, one forward pass compacts (per-edge
    /// test on the copy -- upward inclusion does not close over out-blocks); a closure that
    /// covers the whole buffer returns the buffer itself.
    /// <see cref="DagBuffer{TNode, TEdge}.SourceOrdinal"/> correlates result ordinals back to
    /// the captured stream.</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> TakeUpstreamWhere<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, bool> predicate)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      // Mark: one REVERSE sweep -- dense ordinals ARE a topological order, so every child's
      // inclusion is settled before its parent is reached, and the upward closure propagates
      // by pulling from out-targets in a single pass. Live out-edges (both endpoints in) are
      // counted here, where the children are already settled.
      var included = new bool[nodeCount];
      var includedNodeCount = 0;
      var includedEdgeCount = 0;

      for (var ordinal = nodeCount - 1; ordinal >= 0; ordinal--)
      {
        var reachesAMatch = false;
        var liveOutEdges = 0;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (included[outTargets[slot]])
          {
            reachesAMatch = true;
            liveOutEdges++;
          }
        }

        if (!reachesAMatch && !predicate(buffer[ordinal]))
          continue;

        included[ordinal] = true;
        includedNodeCount++;
        includedEdgeCount += liveOutEdges;
      }

      if (includedNodeCount == nodeCount)
        return buffer;

      // Compact: re-key the included nodes to dense result ordinals (a topological order
      // restricted to any subset keeps parents before children, so the induced sub-dag stays
      // topologically presented) and copy each included node's live out-edges -- the per-edge
      // test the downstream compaction gets to skip.
      var denseByOld = new int[nodeCount];
      var values = new TNode[includedNodeCount];
      var sourceOrdinals = new int[includedNodeCount];
      var dense = true;

      var nextOrdinal = 0;
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!included[ordinal])
          continue;

        denseByOld[ordinal] = nextOrdinal;
        values[nextOrdinal] = buffer[ordinal];
        sourceOrdinals[nextOrdinal] = buffer.SourceOrdinal(ordinal);
        dense &= sourceOrdinals[nextOrdinal] == nextOrdinal;
        nextOrdinal++;
      }

      var offsets = new int[includedNodeCount + 1];
      var targets = new int[includedEdgeCount];
      var payloads = new TEdge[includedEdgeCount];

      var edgeSlot = 0;
      var resultOrdinal = 0;
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!included[ordinal])
          continue;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (!included[outTargets[slot]])
            continue;

          targets[edgeSlot] = denseByOld[outTargets[slot]];
          payloads[edgeSlot] = outPayloads[slot];
          edgeSlot++;
        }

        resultOrdinal++;
        offsets[resultOrdinal] = edgeSlot;
      }

      return new DagBuffer<TNode, TEdge>(
        values,
        new DagStructure<TEdge>(offsets, targets, payloads),
        dense ? null : sourceOrdinals);
    }
  }
}
