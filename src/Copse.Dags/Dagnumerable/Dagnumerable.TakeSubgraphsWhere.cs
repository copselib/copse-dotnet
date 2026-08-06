using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Selects the sub-dag grown from the matching nodes: every match, everything reachable
    /// from a match, and the edges among them -- one result dag, the matches re-rooted
    /// (TakeSubtreesWhere' dag analog; ratified 2026-08-06, docs/DAG_CONTRACT_DESIGN.md THE
    /// SUBGRAPH SELECTION CLUSTER). The tree operator's no-nested-matches flag is EMERGENT
    /// here, not a rule: a match reachable from another match has an in-edge inside the
    /// closure, so it comes out an interior node; the result's sources are exactly the matches
    /// no other match reaches (induced in-degree zero). Shared descendants are shared, never
    /// duplicated -- a second path into included structure is an edge, not a copy. Edges from
    /// OUTSIDE the closure die with their parents (the induced subgraph); because inclusion is
    /// a downward closure, every out-edge of an included node survives whole.
    ///
    /// <para>Per-match separate closures are the caller's loop (call once per match with a
    /// single-node predicate); ancestry-directed selection is the transpose's --
    /// <c>Transpose().TakeSubgraphsWhere(p).Transpose()</c> selects everything that REACHES a
    /// match. The predicate is evaluated at most once per node, and not at all on nodes an
    /// earlier match already swept in (counts unspecified; purity expected).</para>
    ///
    /// <para>Returns a <see cref="DagBuffer{TNode, TEdge}"/> -- capture-shaped BY CONTRACT,
    /// not convenience: the protocol discovers a stream's sources at the start of enumeration,
    /// and this operator's result-sources are discovered by the predicate mid-walk, so a lazy
    /// wrapper cannot honestly present them (the streaming variant is logged as a contract
    /// amendment; see the cluster ruling). One pass marks the closure over the capture's CSR
    /// (dense ordinals are a topological order, so parents settle before children), one pass
    /// compacts; a closure that covers the whole buffer returns the buffer itself.
    /// <see cref="DagBuffer{TNode, TEdge}.SourceOrdinal"/> correlates result ordinals back to
    /// the captured stream.</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> TakeSubgraphsWhere<TNode, TEdge>(
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

      // Mark: one forward sweep -- dense ordinals ARE a topological order, so every parent's
      // inclusion is settled before its children are reached, and the closure propagates in a
      // single pass.
      var included = new bool[nodeCount];
      var includedNodeCount = 0;
      var includedEdgeCount = 0;

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!included[ordinal] && !predicate(buffer[ordinal]))
          continue;

        included[ordinal] = true;
        includedNodeCount++;
        includedEdgeCount += outOffsets[ordinal + 1] - outOffsets[ordinal];

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          included[outTargets[slot]] = true;
      }

      if (includedNodeCount == nodeCount)
        return buffer;

      // Compact: re-key the included nodes to dense result ordinals (a topological order
      // restricted to a downward-closed set is a topological order of the induced sub-dag)
      // and copy the included parents' whole edge blocks -- every target is included by
      // closure, so no per-edge test.
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
