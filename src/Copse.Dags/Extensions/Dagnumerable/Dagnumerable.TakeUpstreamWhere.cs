using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Selects the sub-dag upstream of the matching nodes: every match, everything that
    /// REACHES a match, and the edges among them -- one result dag, the matches its outlets
    /// (<see cref="TakeDownstreamWhere{TNode, TEdge}"/>' flow-reversed mirror -- one
    /// reverse-ordinal pass, no transpose materialized). The mirror of the downstream emergence holds: a match
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

      // Mark: one REVERSE sweep -- dense ordinals ARE a topological order, so every child's
      // inclusion is settled before its parent is reached, and the upward closure propagates
      // by pulling from out-targets in a single pass.
      var included = new bool[nodeCount];

      for (var ordinal = nodeCount - 1; ordinal >= 0; ordinal--)
      {
        var reachesAMatch = false;

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1] && !reachesAMatch; slot++)
          reachesAMatch = included[outTargets[slot]];

        included[ordinal] = reachesAMatch || predicate(buffer[ordinal]);
      }

      return DagCompaction.Compact(buffer, included, keptSlots: null);
    }
  }
}
