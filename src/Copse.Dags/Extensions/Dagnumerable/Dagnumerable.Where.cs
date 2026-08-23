using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The family's <c>Where</c> homolog (design-docs/SUBSTITUTION_TAXONOMY.md,
    /// resolving DAG_CONTRACT_DESIGN.md open question 5): VERTEX BYPASS with caller edge
    /// composition -- the tree's child promotion translated to shared parentage. LINQ
    /// polarity (true = keep). A filtered node DISSOLVES: for each of its (in-edge,
    /// out-edge) pairs a through-edge is manufactured from the in-edge's parent to the
    /// out-edge's child, its payload composed by <paramref name="edgeComposer"/>
    /// (<c>inEdge ∘ outEdge</c> -- e.g. 60% × 50% = 30%; payload composition is domain
    /// semantics, hence the required seat). Chains of filtered nodes compose along each
    /// through-path; parallel result edges are permitted and expected.
    ///
    /// <para>BYPASS, NOT REMOVAL -- no liveness: kept nodes never die. A kept node whose
    /// every in-path ran through filtered SOURCES simply becomes a source -- the tree's
    /// filtered-root promotion, dag-side. (Graph theory's own name for the operation is
    /// vertex bypass / smoothing -- not contraction, which merges endpoints and needs the value
    /// identity this library never asks for.)</para>
    ///
    /// <para>COST CLASS, stated honestly: a filtered node manufactures in-degree ×
    /// out-degree edges; a filtered REGION manufactures one edge per through-path. The
    /// output is big because the answer is big.</para>
    ///
    /// <para>Capture-shaped: a manufactured through-edge is learned at the filtered node's entry,
    /// long after its origin parent's dispatch block closed, and emitting it there would break
    /// the DISPATCH CONTIGUITY clause the streaming edge wrappers rely on.</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> Where<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, bool> predicate,
      Func<TEdge, TEdge, TEdge> edgeComposer)
    {
      if (predicate == null)
        throw new ArgumentNullException(nameof(predicate));
      if (edgeComposer == null)
        throw new ArgumentNullException(nameof(edgeComposer));

      var buffer = DagBuffer<TNode, TEdge>.From(source);
      var structure = buffer.Structure;
      var nodeCount = structure.NodeCount;
      var outOffsets = structure.OutOffsets;
      var outTargets = structure.OutTargets;
      var outPayloads = structure.OutPayloads;

      // The verdict pass: once per node, in topological order (purity expected, the house
      // contract). Every node is consulted -- bypass severs nothing, so nothing is dead.
      var keep = new bool[nodeCount];
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        keep[ordinal] = predicate(buffer[ordinal]);

      // Placement: kept nodes in original topological order, seats preserved.
      var resultOrdinalOf = new int[nodeCount];
      var resultValues = new List<TNode>();
      var resultSourceOrdinals = new List<int>();

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!keep[ordinal])
        {
          resultOrdinalOf[ordinal] = -1;
          continue;
        }

        resultOrdinalOf[ordinal] = resultValues.Count;
        resultValues.Add(buffer[ordinal]);
        resultSourceOrdinals.Add(buffer.SourceOrdinal(ordinal));
      }

      // The out-blocks: each kept origin's out-edges expanded depth-first through filtered
      // children at their original positions -- a kept child stays a direct edge; a filtered
      // child is traversed, composing payloads along the way, until kept descendants are
      // reached. Depth-first at the filtered child's seat is the tree promotion's
      // presentation (children promoted in order, in place). Explicit frames keep long
      // filtered chains off the call stack.
      var resultOffsets = new List<int>(resultValues.Count + 1) { 0 };
      var resultTargets = new List<int>();
      var resultPayloads = new List<TEdge>();
      var frames = new List<BypassFrame<TEdge>>();

      for (var origin = 0; origin < nodeCount; origin++)
      {
        if (!keep[origin])
          continue;

        frames.Add(new BypassFrame<TEdge>(origin, outOffsets[origin], hasCarried: false, carried: default));

        while (frames.Count > 0)
        {
          var frameIndex = frames.Count - 1;
          var frame = frames[frameIndex];

          if (frame.NextSlot == outOffsets[frame.Node + 1])
          {
            frames.RemoveAt(frameIndex);
            continue;
          }

          var slot = frame.NextSlot;
          frame.NextSlot++;
          frames[frameIndex] = frame;

          var child = outTargets[slot];
          var payload = frame.HasCarried ? edgeComposer(frame.Carried, outPayloads[slot]) : outPayloads[slot];

          if (keep[child])
          {
            resultTargets.Add(resultOrdinalOf[child]);
            resultPayloads.Add(payload);
            continue;
          }

          frames.Add(new BypassFrame<TEdge>(child, outOffsets[child], hasCarried: true, carried: payload));
        }

        resultOffsets.Add(resultTargets.Count);
      }

      return DagBuffer<TNode, TEdge>.FromParts(
        resultValues.ToArray(),
        new DagStructure<TEdge>(resultOffsets.ToArray(), resultTargets.ToArray(), resultPayloads.ToArray()),
        resultSourceOrdinals.ToArray());
    }

    private struct BypassFrame<TEdge>
    {
      public BypassFrame(int node, int nextSlot, bool hasCarried, TEdge carried)
      {
        Node = node;
        NextSlot = nextSlot;
        HasCarried = hasCarried;
        Carried = carried;
      }

      public readonly int Node;
      public int NextSlot;
      public readonly bool HasCarried;
      public readonly TEdge Carried;
    }
  }
}
