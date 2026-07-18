using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The survey-shaped UPWARD pass -- per-owner attribution through shared entities, the
    /// diamond's anti-double-count (a naive upward sum counts a shared subtree once per
    /// parent; here each node decides what travels up EACH in-edge, so what a child sent up an
    /// edge IS that parent's share, by construction). Each node resolves as a
    /// <see cref="DagDispatchNode{TNode, TDispatch, TEdge}"/> -- its value plus the edge-paired
    /// upflows its children dispatched to it (empty at sinks: value originates IN the nodes, so
    /// there is no seed, the downward pass's dual asymmetry) -- and, when it has live in-edges,
    /// <paramref name="survey"/> is handed the resolved node and one exactly-once
    /// <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/> per in-edge (in discovery
    /// order) to write. Sources are never surveyed; their resolved inflows ARE the attribution
    /// result. Rides one forward capture folded in reverse topological order; returns a
    /// MATERIALIZED composite, shape-isomorphic, decorate-then-choose downstream.
    /// </summary>
    public static Dag<DagDispatchNode<TNode, TDispatch, TEdge>, TEdge> LeaffixDispatch<TNode, TDispatch, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Action<DagDispatchNode<TNode, TDispatch, TEdge>, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      var capture = DagCapture<TNode, TEdge>.From(source);
      var nodesByOrdinal = new Dictionary<int, DagDispatchNode<TNode, TDispatch, TEdge>>();
      var upflowsByOrdinal = new Dictionary<int, List<DagInflow<TDispatch, TEdge>>>();
      var assembler = new DagAssembler<DagDispatchNode<TNode, TDispatch, TEdge>, TEdge>();

      for (var index = capture.Entries.Count - 1; index >= 0; index--)
      {
        var (ordinal, value) = capture.Entries[index];

        IReadOnlyList<DagInflow<TDispatch, TEdge>> upflows =
          upflowsByOrdinal.TryGetValue(ordinal, out var arrived)
            ? arrived
            : Array.Empty<DagInflow<TDispatch, TEdge>>();

        var dispatchNode = new DagDispatchNode<TNode, TDispatch, TEdge>(value, upflows);
        nodesByOrdinal[ordinal] = dispatchNode;

        if (!capture.InEdges.TryGetValue(ordinal, out var inEdges))
          continue;

        var targets = new List<DagDispatchTarget<TNode, TDispatch, TEdge>>(inEdges.Count);
        foreach (var (parentOrdinal, edge) in inEdges)
          targets.Add(new DagDispatchTarget<TNode, TDispatch, TEdge>(capture.Values[parentOrdinal], edge, parentOrdinal));

        survey(dispatchNode, targets);

        foreach (var target in targets)
        {
          if (!target.IsDispatched)
            throw new InvalidOperationException($"The edge to '{target.Value}' was not dispatched.");

          if (!upflowsByOrdinal.TryGetValue(target.TargetOrdinal, out var parentUpflows))
            upflowsByOrdinal[target.TargetOrdinal] = parentUpflows = new List<DagInflow<TDispatch, TEdge>>();

          parentUpflows.Add(new DagInflow<TDispatch, TEdge>(target.DispatchedValue, target.Edge));
        }
      }

      foreach (var (ordinal, _) in capture.Entries)
        assembler.AddNode(ordinal, nodesByOrdinal[ordinal]);
      capture.WireStructure(assembler);

      return assembler.Build();
    }
  }
}
