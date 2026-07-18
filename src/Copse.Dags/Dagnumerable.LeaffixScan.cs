using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Upward cumulative scan over the contract: <paramref name="accumulate"/> receives each
    /// node and one EDGE-PAIRED result per live out-edge -- the child's accumulated result with
    /// the payload of the edge to it, in out-edge order; empty at sinks, which is the call that
    /// seeds the scan. Each node is computed exactly once no matter how many parents share it;
    /// a shared child's (single, reused) result appears in EACH parent's list, and parallel
    /// edges contribute it twice -- the diamond question stays the caller's explicit choice
    /// (combine per-edge results for per-use roll-ups; use
    /// <see cref="LeaffixDispatch{TNode, TDispatch, TEdge}"/> for attribution that must not
    /// double-count). Rides one forward capture folded in reverse topological order (a leaffix
    /// result is children-first by definition, so the whole graph precedes the first result;
    /// the backward stream cannot carry per-parent out-edge order, so the capture is also what
    /// keeps the result shape-isomorphic). Returns a MATERIALIZED composite.
    /// </summary>
    public static Dag<TResult, TEdge> LeaffixScan<TNode, TResult, TEdge>(
      this IForwardDagnumerable<TNode, TEdge> source,
      Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
    {
      if (accumulate == null)
        throw new ArgumentNullException(nameof(accumulate));

      var capture = DagCapture<TNode, TEdge>.From(source);
      var resultsByOrdinal = new Dictionary<int, TResult>();
      var assembler = new DagAssembler<TResult, TEdge>();

      for (var index = capture.Entries.Count - 1; index >= 0; index--)
      {
        var (ordinal, value) = capture.Entries[index];

        IReadOnlyList<DagInflow<TResult, TEdge>> childResults;
        if (capture.OutEdges.TryGetValue(ordinal, out var outEdges))
        {
          var results = new List<DagInflow<TResult, TEdge>>(outEdges.Count);
          foreach (var (childOrdinal, edge) in outEdges)
            results.Add(new DagInflow<TResult, TEdge>(resultsByOrdinal[childOrdinal], edge));
          childResults = results;
        }
        else
        {
          childResults = Array.Empty<DagInflow<TResult, TEdge>>();
        }

        resultsByOrdinal[ordinal] = accumulate(value, childResults);
      }

      foreach (var (ordinal, _) in capture.Entries)
        assembler.AddNode(ordinal, resultsByOrdinal[ordinal]);
      capture.WireStructure(assembler);

      return assembler.Build();
    }
  }
}
