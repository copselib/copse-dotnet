using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Selects the sub-dag downstream of the matching nodes: every match, everything reachable
    /// from a match, and the edges among them -- one result dag, the matches re-rooted
    /// (TakeSubtreesWhere's dag analog; a subgraph is any subset, so the name says the flow
    /// direction). The tree operator's
    /// no-nested-matches flag is EMERGENT here, not a rule: a match reachable from another
    /// match has an in-edge inside the closure, so it comes out an interior node; the result's
    /// sources are exactly the matches no other match reaches (induced in-degree zero). Shared
    /// descendants are shared, never duplicated -- a second path into included structure is an
    /// edge, not a copy. Edges from OUTSIDE the closure die with their parents (the induced
    /// subgraph); because inclusion is a downward closure, every out-edge of an included node
    /// survives whole.
    ///
    /// <para>Per-match separate closures are the caller's loop (call once per match with a
    /// single-node predicate); the upstream mirror is
    /// <see cref="TakeUpstreamWhere{TNode, TEdge}"/> -- everything that REACHES a match --
    /// with the transpose law <c>TakeUpstreamWhere(p) ≡
    /// Transpose().TakeDownstreamWhere(p).Transpose()</c> pinned in the battery. The
    /// predicate is evaluated at most once per node, and not at all on nodes an earlier match
    /// already swept in (counts unspecified; purity expected).</para>
    ///
    /// <para>Returns a <see cref="DagBuffer{TNode, TEdge}"/> -- capture-shaped BY CONTRACT,
    /// not convenience: the protocol discovers a stream's sources at the start of enumeration,
    /// and this operator's result-sources are discovered by the predicate mid-walk, so a lazy
    /// wrapper cannot honestly present them (a streaming variant would amend the
    /// sources-are-fixed contract). One pass marks the closure over the capture's CSR
    /// (dense ordinals are a topological order, so parents settle before children), one pass
    /// compacts; a closure that covers the whole buffer returns the buffer itself.
    /// <see cref="DagBuffer{TNode, TEdge}.SourceOrdinal"/> correlates result ordinals back to
    /// the captured stream.</para>
    /// </summary>
    public static DagBuffer<TNode, TEdge> TakeDownstreamWhere<TNode, TEdge>(
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

      // Mark: one forward sweep -- dense ordinals ARE a topological order, so every parent's
      // inclusion is settled before its children are reached, and the closure propagates in a
      // single pass.
      var included = new bool[nodeCount];

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (!included[ordinal] && !predicate(buffer[ordinal]))
          continue;

        included[ordinal] = true;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
          included[outTargets[slot]] = true;
      }

      return DagCompaction.Compact(buffer, included, keptSlots: null);
    }
  }
}
