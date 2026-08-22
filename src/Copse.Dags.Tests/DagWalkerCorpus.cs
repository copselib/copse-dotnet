using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Dags.Tests
{
  // The walker tier's shared fixtures and content helpers: the canonical shapes (the ownership
  // diamond, the chain, two islands, the shared leaf under three parents, the empty dag) and the
  // content-canonical readings every content pin compares on -- node multiset, edge multiset
  // with payloads, and the source list -- because operator differentials are CONTENT-based,
  // never stream-based (the dag contract's rule).
  internal static class DagWalkerCorpus
  {
    // apex owns left 60% / right 40%; each owns the venture (70%/30%).
    public static Dag<string, decimal> Diamond()
    {
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild("left", 0.60m);
      var right = apex.AddChild("right", 0.40m);
      var venture = new DagNode<string, decimal>("venture");
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);
      return new Dag<string, decimal>(apex);
    }

    public static Dag<string, decimal> Chain()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m).AddChild("c", 1m);
      return new Dag<string, decimal>(a);
    }

    public static Dag<string, decimal> TwoIslands()
    {
      var island1 = new DagNode<string, decimal>("island1");
      island1.AddChild("island1Child", 1m);
      var island2 = new DagNode<string, decimal>("island2");
      return new Dag<string, decimal>(island1, island2);
    }

    // alpha and beta are sources; sharedLeaf has three parents, two of them sources.
    public static Dag<string, decimal> SharedLeaf()
    {
      var alpha = new DagNode<string, decimal>("alpha");
      var beta = new DagNode<string, decimal>("beta");
      var middle = alpha.AddChild("middle", 0.5m);
      var sharedLeaf = new DagNode<string, decimal>("sharedLeaf");
      alpha.AddChild(sharedLeaf, 0.1m);
      beta.AddChild(sharedLeaf, 0.2m);
      middle.AddChild(sharedLeaf, 0.3m);
      return new Dag<string, decimal>(alpha, beta);
    }

    public static Dag<string, decimal> Empty() => new Dag<string, decimal>();

    public static IEnumerable<(string Name, Func<Dag<string, decimal>> Factory)> All()
    {
      yield return ("diamond", Diamond);
      yield return ("chain", Chain);
      yield return ("twoIslands", TwoIslands);
      yield return ("sharedLeaf", SharedLeaf);
      yield return ("empty", Empty);
    }

    public static List<string> Edges(IDagnumerable<string, decimal> dag)
      => dag.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge:0.00}").OrderBy(text => text, StringComparer.Ordinal).ToList();

    public static List<string> Nodes(IDagnumerable<string, decimal> dag)
      => dag.GetTopologicalOrder().OrderBy(node => node, StringComparer.Ordinal).ToList();

    public static List<string> Sources(IDagnumerable<string, decimal> dag)
      => dag.GetSources().OrderBy(node => node, StringComparer.Ordinal).ToList();

    // The content-canonical reading: nodes, edges with payloads, and the source SET (source
    // order is a presentation fact -- the materialized Transpose() reverses it, lenses present
    // discovery order -- so content pins read it sorted).
    public static string Content(IDagnumerable<string, decimal> dag)
      => $"nodes[{string.Join(",", Nodes(dag))}] edges[{string.Join(",", Edges(dag))}] sources[{string.Join(",", Sources(dag))}]";

    // The visit stream read up to the correlation key's relabeling -- ordinals are a stable
    // per-enumeration key, not a coordinate (a topology walk mints its own at discovery; the
    // buffer presents its dense handles), so the degenerate-tower pins compare the visit
    // SEQUENCE: modes, nodes, dispatching parents, edge indices, payloads.
    public static List<string> Stream(IDagnumerable<string, decimal> dag)
    {
      var visits = new List<string>();
      var nodeByOrdinal = new Dictionary<int, string>();
      using var walk = dag.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
      {
        nodeByOrdinal[walk.Ordinal] = walk.Node;
        visits.Add(walk.Mode == DagnumeratorMode.EnteringNode
          ? $"E:{walk.Node}"
          : $"D:{walk.Node}<{(walk.ParentOrdinal < 0 ? "virtual" : nodeByOrdinal[walk.ParentOrdinal])}#{walk.EdgeIndex}:{walk.Edge:0.00}");
      }
      return visits;
    }
  }
}
