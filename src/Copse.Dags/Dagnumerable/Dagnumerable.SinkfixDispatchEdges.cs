using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-result flavor of the upward survey pass (docs/DAG_CONTRACT_DESIGN.md, the edge
    /// dual, tier 2): what each survey dispatches BECOMES the result's edge payloads. Every
    /// node with in-edges is surveyed once, in reverse topological order, receiving its
    /// resolved decoration -- the node value plus its OUT-edges' already-rewritten payloads,
    /// children's writes arriving as inflows (empty at sinks) -- and one exactly-once
    /// <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/> per IN-edge (parent value, old
    /// payload; discovery order). Sources are never surveyed (no in-edges), yet every edge is
    /// written exactly once: each edge is precisely one non-source node's in-edge.
    ///
    /// <para>This is the general-purpose GROUP-scoped edge writer: a node's in-edge group is a
    /// constrained unit (a distribution, where payloads are weights), and rewriting it demands
    /// the whole group in scope -- conditioning (drop an outcome, renormalize the survivors),
    /// rebalancing, normalization are all one survey lambda; the caller owns the payload
    /// algebra, the library owns completeness, order, and strictness. Returns a MATERIALIZED
    /// shape-isomorphic composite: node values carried unchanged, payloads replaced.</para>
    /// </summary>
    public static Dag<TNode, TEdgeResult> SinkfixDispatchEdges<TNode, TEdge, TEdgeResult>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Action<DagDispatchNode<TNode, TEdgeResult, TEdge>, IReadOnlyList<DagDispatchTarget<TNode, TEdgeResult, TEdge>>> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      var capture = DagCapture<TNode, TEdge>.From(source);
      var newPayloadsByOrdinal = new Dictionary<int, TEdgeResult[]>();
      var outEdgeResultsByOrdinal = new Dictionary<int, List<DagDispatchInflow<TNode, TEdgeResult, TEdge>>>();

      for (var index = capture.Entries.Count - 1; index >= 0; index--)
      {
        var (ordinal, value) = capture.Entries[index];

        IReadOnlyList<DagDispatchInflow<TNode, TEdgeResult, TEdge>> outEdgeResults =
          outEdgeResultsByOrdinal.TryGetValue(ordinal, out var written)
            ? written
            : Array.Empty<DagDispatchInflow<TNode, TEdgeResult, TEdge>>();

        var resolved = new DagDispatchNode<TNode, TEdgeResult, TEdge>(
          value, outEdgeResults, isSource: !capture.InEdges.ContainsKey(ordinal));

        if (!capture.InEdges.TryGetValue(ordinal, out var inEdges))
          continue;

        var targets = new List<DagDispatchTarget<TNode, TEdgeResult, TEdge>>(inEdges.Count);
        foreach (var (parentOrdinal, edge) in inEdges)
          targets.Add(new DagDispatchTarget<TNode, TEdgeResult, TEdge>(capture.Values[parentOrdinal], edge, parentOrdinal));

        survey(resolved, targets);

        var newPayloads = new TEdgeResult[inEdges.Count];

        for (var inEdgeIndex = 0; inEdgeIndex < inEdges.Count; inEdgeIndex++)
        {
          var target = targets[inEdgeIndex];

          if (!target.IsDispatched)
            throw new InvalidOperationException($"The edge from '{target.Value}' was not dispatched.");

          newPayloads[inEdgeIndex] = target.DispatchedValue;

          // The cascade: this node's write becomes visible to its parent as an out-edge result.
          if (!outEdgeResultsByOrdinal.TryGetValue(target.TargetOrdinal, out var parentResults))
            outEdgeResultsByOrdinal[target.TargetOrdinal] = parentResults = new List<DagDispatchInflow<TNode, TEdgeResult, TEdge>>();

          parentResults.Add(new DagDispatchInflow<TNode, TEdgeResult, TEdge>(value, target.DispatchedValue, target.Edge));
        }

        newPayloadsByOrdinal[ordinal] = newPayloads;
      }

      return RebuildWithNewPayloads(capture, newPayloadsByOrdinal);
    }

    // Rebuild in the ORIGINAL orientation and per-parent out-edge order, pulling each edge's new
    // payload from the child side, where it was written. The k-th out-edge from parent P to
    // child C corresponds to C's k-th in-edge from P (per-parent dispatch is contiguous and in
    // order), so a per-(parent, child) cursor over the child's in-edge list resolves parallel
    // edges unambiguously -- never by payload comparison.
    private static Dag<TNode, TEdgeResult> RebuildWithNewPayloads<TNode, TEdge, TEdgeResult>(
      DagCapture<TNode, TEdge> capture,
      Dictionary<int, TEdgeResult[]> newPayloadsByOrdinal)
    {
      var assembler = new DagAssembler<TNode, TEdgeResult>();

      foreach (var sourceOrdinal in capture.Sources)
        assembler.AddSource(sourceOrdinal);

      foreach (var (ordinal, value) in capture.Entries)
        assembler.AddNode(ordinal, value);

      var inEdgeCursors = new Dictionary<(int ChildOrdinal, int ParentOrdinal), int>();

      foreach (var (parentOrdinal, _) in capture.Entries)
      {
        if (!capture.OutEdges.TryGetValue(parentOrdinal, out var outEdges))
          continue;

        foreach (var (childOrdinal, _) in outEdges)
        {
          var childInEdges = capture.InEdges[childOrdinal];
          inEdgeCursors.TryGetValue((childOrdinal, parentOrdinal), out var searchFrom);

          var inEdgeIndex = searchFrom;
          while (childInEdges[inEdgeIndex].ParentOrdinal != parentOrdinal)
            inEdgeIndex++;

          inEdgeCursors[(childOrdinal, parentOrdinal)] = inEdgeIndex + 1;
          assembler.AddEdge(parentOrdinal, childOrdinal, newPayloadsByOrdinal[childOrdinal][inEdgeIndex]);
        }
      }

      return assembler.Build();
    }
  }
}
