using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The edge-result flavor of the downward survey pass -- <see cref="SinkfixDispatchEdges"/>'s
    /// mirror (docs/DAG_CONTRACT_DESIGN.md, the edge dual, tier 2): what each survey dispatches
    /// BECOMES the result's edge payloads. Every node with out-edges is surveyed once, in
    /// forward topological order, receiving its resolved decoration -- the node value plus its
    /// IN-edges' already-rewritten payloads, ancestors' writes arriving as inflows (empty at
    /// sources) -- and one exactly-once <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/>
    /// per OUT-edge (child value, old payload; out-edge order). Sinks are never surveyed, yet
    /// every edge is written exactly once: each edge is precisely one non-sink node's out-edge.
    /// The natural home of path-cumulative edge values (ownership carried TO each edge); the
    /// upward twin owns in-edge group algebra (conditioning, rebalancing). Returns a
    /// MATERIALIZED shape-isomorphic composite: node values carried unchanged, payloads
    /// replaced.
    /// </summary>
    public static Dag<TNode, TEdgeResult> SourcefixDispatchEdges<TNode, TEdge, TEdgeResult>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Action<DagDispatchNode<TNode, TEdgeResult, TEdge>, IReadOnlyList<DagDispatchTarget<TNode, TEdgeResult, TEdge>>> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      var capture = DagCapture<TNode, TEdge>.From(source);
      var newPayloadsByOrdinal = new Dictionary<int, TEdgeResult[]>();
      var inEdgeResultsByOrdinal = new Dictionary<int, List<DagDispatchInflow<TNode, TEdgeResult, TEdge>>>();

      foreach (var (ordinal, value) in capture.Entries)
      {
        IReadOnlyList<DagDispatchInflow<TNode, TEdgeResult, TEdge>> inEdgeResults =
          inEdgeResultsByOrdinal.TryGetValue(ordinal, out var written)
            ? written
            : Array.Empty<DagDispatchInflow<TNode, TEdgeResult, TEdge>>();

        var resolved = new DagDispatchNode<TNode, TEdgeResult, TEdge>(
          value, inEdgeResults, isSource: !capture.InEdges.ContainsKey(ordinal));

        if (!capture.OutEdges.TryGetValue(ordinal, out var outEdges))
          continue;

        var targets = new List<DagDispatchTarget<TNode, TEdgeResult, TEdge>>(outEdges.Count);
        foreach (var (childOrdinal, edge) in outEdges)
          targets.Add(new DagDispatchTarget<TNode, TEdgeResult, TEdge>(capture.Values[childOrdinal], edge, childOrdinal));

        survey(resolved, targets);

        var newPayloads = new TEdgeResult[outEdges.Count];

        for (var outEdgeIndex = 0; outEdgeIndex < outEdges.Count; outEdgeIndex++)
        {
          var target = targets[outEdgeIndex];

          if (!target.IsDispatched)
            throw new InvalidOperationException($"The edge to '{target.Value}' was not dispatched.");

          newPayloads[outEdgeIndex] = target.DispatchedValue;

          // The cascade: this node's write becomes visible to its child as an in-edge result.
          if (!inEdgeResultsByOrdinal.TryGetValue(target.TargetOrdinal, out var childResults))
            inEdgeResultsByOrdinal[target.TargetOrdinal] = childResults = new List<DagDispatchInflow<TNode, TEdgeResult, TEdge>>();

          childResults.Add(new DagDispatchInflow<TNode, TEdgeResult, TEdge>(value, target.DispatchedValue, target.Edge));
        }

        newPayloadsByOrdinal[ordinal] = newPayloads;
      }

      // Rebuild in original orientation; payloads were written per parent's out-edge group, so
      // the slots index directly -- no correlation needed.
      var assembler = new DagAssembler<TNode, TEdgeResult>();

      foreach (var sourceOrdinal in capture.Sources)
        assembler.AddSource(sourceOrdinal);

      foreach (var (ordinal, value) in capture.Entries)
        assembler.AddNode(ordinal, value);

      foreach (var (parentOrdinal, _) in capture.Entries)
        if (capture.OutEdges.TryGetValue(parentOrdinal, out var outEdges))
          for (var outEdgeIndex = 0; outEdgeIndex < outEdges.Count; outEdgeIndex++)
            assembler.AddEdge(parentOrdinal, outEdges[outEdgeIndex].ChildOrdinal, newPayloadsByOrdinal[parentOrdinal][outEdgeIndex]);

      return assembler.Build();
    }
  }
}
