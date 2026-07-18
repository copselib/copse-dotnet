using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Downward cumulative scan over the contract: <paramref name="accumulate"/> receives each
    /// node and one EDGE-PAIRED inflow per live in-edge -- the parent's accumulated result with
    /// the payload it arrived on, in discovery order; empty at sources, which is the call that
    /// seeds the scan. Runs the forward pass NOW, streaming (each node computed exactly once,
    /// at entry, inflows complete by protocol guarantee), and returns the results as a
    /// MATERIALIZED shape-isomorphic composite -- a scan's value is an entry-time fact, so a
    /// lazy dag of results cannot honestly exist (docs/DAG_CONTRACT_DESIGN.md, open question 7,
    /// ratified). The materialization is an upgrade: the result affords both dimensions.
    /// </summary>
    public static Dag<TResult, TEdge> RootfixScan<TNode, TResult, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
    {
      if (accumulate == null)
        throw new ArgumentNullException(nameof(accumulate));

      var resultsByOrdinal = new Dictionary<int, TResult>();
      var inflowsByOrdinal = new Dictionary<int, List<DagInflow<TResult, TEdge>>>();
      var assembler = new DagAssembler<TResult, TEdge>();

      using var walk = source.GetForwardDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.DiscoveringNode)
        {
          if (walk.ParentOrdinal < 0)
          {
            assembler.AddSource(walk.Ordinal);
            continue;
          }

          // The dispatching parent has entered, so its result exists -- the scan copies it
          // down every out-edge, paired with the edge it rides.
          if (!inflowsByOrdinal.TryGetValue(walk.Ordinal, out var inflows))
            inflowsByOrdinal[walk.Ordinal] = inflows = new List<DagInflow<TResult, TEdge>>();

          inflows.Add(new DagInflow<TResult, TEdge>(resultsByOrdinal[walk.ParentOrdinal], walk.Edge));
          assembler.AddEdge(walk.ParentOrdinal, walk.Ordinal, walk.Edge);
          continue;
        }

        var nodeInflows = inflowsByOrdinal.TryGetValue(walk.Ordinal, out var arrived)
          ? (IReadOnlyList<DagInflow<TResult, TEdge>>)arrived
          : Array.Empty<DagInflow<TResult, TEdge>>();

        var result = accumulate(walk.Node, nodeInflows);
        resultsByOrdinal[walk.Ordinal] = result;
        assembler.AddNode(walk.Ordinal, result);
      }

      return assembler.Build();
    }
  }
}
