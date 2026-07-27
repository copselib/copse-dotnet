using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The survey-shaped downward pass over the contract (the work API's shape: a
    /// setter-callback allocator plugs in verbatim): each node resolves as a
    /// <see cref="DagDispatchNode{TNode, TDispatch, TEdge}"/> -- its value plus its edge-paired
    /// inflows, sources receiving the single seeded inflow -- and, when it has live out-edges,
    /// <paramref name="survey"/> is handed the resolved node and one
    /// <see cref="DagDispatchTarget{TNode, TDispatch, TEdge}"/> per edge to write EXACTLY once
    /// (unwritten and double-written slots throw). What each target receives becomes that
    /// child's inflow on that edge. Only live edges are surveyed, so pruning blockers upstream
    /// composes into the allocation for free. Runs the forward pass NOW and returns the
    /// decorated dag as a MATERIALIZED composite (docs/DAG_CONTRACT_DESIGN.md, open question 7,
    /// ratified): choose the view downstream -- <c>.Select(dispatchNode =&gt; ...)</c> for
    /// values, <see cref="DagDispatchNode{TNode, TDispatch, TEdge}.Inflows"/> for attribution.
    /// </summary>
    public static Dag<DagDispatchNode<TNode, TDispatch, TEdge>, TEdge> SourcefixDispatch<TNode, TDispatch, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      TDispatch seed,
      Action<DagDispatchNode<TNode, TDispatch, TEdge>, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>> survey)
    {
      if (survey == null)
        throw new ArgumentNullException(nameof(survey));

      var nodesByOrdinal = new Dictionary<int, DagDispatchNode<TNode, TDispatch, TEdge>>();
      var inflowsByOrdinal = new Dictionary<int, List<DagDispatchInflow<TNode, TDispatch, TEdge>>>();
      var sourceOrdinals = new HashSet<int>();
      var assembler = new DagAssembler<DagDispatchNode<TNode, TDispatch, TEdge>, TEdge>();

      // The dispatch block in flight: the last entered node and the live out-edges its entry
      // has dispatched so far. A block closes -- the survey runs, outflows land as the
      // targets' inflows -- when the next entry arrives (or the stream ends): the protocol
      // dispatches contiguously after each entry, and the survey needs the COMPLETE edge list
      // (allocation is a fairness statement over all of them at once).
      var dispatchingOrdinal = -1;
      var targets = new List<DagDispatchTarget<TNode, TDispatch, TEdge>>();

      void CloseDispatchBlock()
      {
        if (dispatchingOrdinal < 0 || targets.Count == 0)
          return;

        survey(nodesByOrdinal[dispatchingOrdinal], targets);

        foreach (var target in targets)
        {
          if (!target.IsDispatched)
            throw new InvalidOperationException($"The edge to '{target.Value}' was not dispatched.");

          if (!inflowsByOrdinal.TryGetValue(target.TargetOrdinal, out var inflows))
            inflowsByOrdinal[target.TargetOrdinal] = inflows = new List<DagDispatchInflow<TNode, TDispatch, TEdge>>();

          inflows.Add(new DagDispatchInflow<TNode, TDispatch, TEdge>(nodesByOrdinal[dispatchingOrdinal].Value, target.DispatchedValue, target.Edge));
        }

        targets = new List<DagDispatchTarget<TNode, TDispatch, TEdge>>();
      }

      using var walk = source.GetForwardDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.DiscoveringNode)
        {
          if (walk.ParentOrdinal < 0)
          {
            sourceOrdinals.Add(walk.Ordinal);
            assembler.AddSource(walk.Ordinal);
            continue;
          }

          if (walk.ParentOrdinal != dispatchingOrdinal)
            throw new InvalidOperationException(
              "Non-contiguous dispatch: a discovery arrived from a node other than the last entered one.");

          targets.Add(new DagDispatchTarget<TNode, TDispatch, TEdge>(walk.Node, walk.Edge, walk.Ordinal));
          assembler.AddEdge(walk.ParentOrdinal, walk.Ordinal, walk.Edge);
          continue;
        }

        CloseDispatchBlock();

        IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>> nodeInflows =
          sourceOrdinals.Contains(walk.Ordinal)
            ? new[] { new DagDispatchInflow<TNode, TDispatch, TEdge>(default, seed, default) }
            : inflowsByOrdinal.TryGetValue(walk.Ordinal, out var arrived)
              ? arrived
              : Array.Empty<DagDispatchInflow<TNode, TDispatch, TEdge>>();

        var dispatchNode = new DagDispatchNode<TNode, TDispatch, TEdge>(walk.Node, nodeInflows, isSource: sourceOrdinals.Contains(walk.Ordinal));
        nodesByOrdinal[walk.Ordinal] = dispatchNode;
        assembler.AddNode(walk.Ordinal, dispatchNode);
        dispatchingOrdinal = walk.Ordinal;
      }

      CloseDispatchBlock();
      return assembler.Build();
    }
  }
}
