using System.Collections.Generic;

namespace Copse.Dags
{
  // The upward passes' shared substrate (docs/DAG_CONTRACT_DESIGN.md, phase 3): ONE forward
  // walk captured into ordinal-keyed structure, folded in reverse entry order (reverse
  // topological -- children complete before parents). The leaffix family rides this rather
  // than the backward walk for an information-theoretic reason: original orientation and
  // per-parent out-edge ORDER are forward-stream facts -- the backward stream carries a
  // node's in-edge order (its dispatch list is the transpose's), never its out-edge order --
  // and the result dags must be shape-isomorphic to the source. The capture is not a new cost
  // class: leaffix results are children-first by definition, so the whole graph precedes the
  // first result no matter how it is walked (the tree family's capture-then-fold pattern).
  internal sealed class DagCapture<TNode, TEdge>
  {
    private DagCapture()
    {
    }

    /// <summary>Entries in entry (topological) order: the fold iterates this reversed.</summary>
    public List<(int Ordinal, TNode Value)> Entries { get; } = new();

    /// <summary>Per-node live out-edges, in dispatch (out-edge) order.</summary>
    public Dictionary<int, List<(int ChildOrdinal, TEdge Edge)>> OutEdges { get; } = new();

    /// <summary>Per-node live in-edges, in discovery order.</summary>
    public Dictionary<int, List<(int ParentOrdinal, TEdge Edge)>> InEdges { get; } = new();

    /// <summary>Source ordinals in conventional-discovery order (the result dag's root order).</summary>
    public List<int> Sources { get; } = new();

    /// <summary>Source values by ordinal (dispatch targets name their node).</summary>
    public Dictionary<int, TNode> Values { get; } = new();

    public static DagCapture<TNode, TEdge> From(IForwardDagnumerable<TNode, TEdge> source)
    {
      var capture = new DagCapture<TNode, TEdge>();

      using var walk = source.GetForwardDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        if (walk.Mode == DagnumeratorMode.EnteringNode)
        {
          capture.Entries.Add((walk.Ordinal, walk.Node));
          capture.Values[walk.Ordinal] = walk.Node;
          continue;
        }

        if (walk.ParentOrdinal < 0)
        {
          capture.Sources.Add(walk.Ordinal);
          continue;
        }

        if (!capture.OutEdges.TryGetValue(walk.ParentOrdinal, out var outEdges))
          capture.OutEdges[walk.ParentOrdinal] = outEdges = new List<(int, TEdge)>();
        outEdges.Add((walk.Ordinal, walk.Edge));

        if (!capture.InEdges.TryGetValue(walk.Ordinal, out var inEdges))
          capture.InEdges[walk.Ordinal] = inEdges = new List<(int, TEdge)>();
        inEdges.Add((walk.ParentOrdinal, walk.Edge));
      }

      return capture;
    }

    /// <summary>Wires an assembler with this capture's structure (sources + edges, order preserved).</summary>
    public void WireStructure<TResult>(DagAssembler<TResult, TEdge> assembler)
    {
      foreach (var sourceOrdinal in Sources)
        assembler.AddSource(sourceOrdinal);

      foreach (var (parentOrdinal, _) in Entries)
        if (OutEdges.TryGetValue(parentOrdinal, out var outEdges))
          foreach (var (childOrdinal, edge) in outEdges)
            assembler.AddEdge(parentOrdinal, childOrdinal, edge);
    }
  }
}
