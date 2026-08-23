using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // WHERE, the family homolog (design-docs/SUBSTITUTION_TAXONOMY.md, resolving
  // DAG_CONTRACT_DESIGN.md open question 5): vertex bypass with caller edge composition,
  // LINQ polarity (true = keep). The load-bearing pins: through-edges compose in-edge ∘
  // out-edge per path (left-fold along filtered chains); BYPASS IS NOT REMOVAL -- kept nodes
  // never die, and a kept node whose in-paths all ran through filtered sources becomes a
  // source (the tree's filtered-root promotion); parallel result edges are expected;
  // lookthrough is bypass-invariant (dissolving a pass-through entity changes presentation,
  // never arithmetic).
  [TestClass]
  public class DagWhereTests
  {
    private static Dag<string, decimal> Diamond()
    {
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild("left", 0.60m);
      var right = apex.AddChild("right", 0.40m);
      var venture = new DagNode<string, decimal>("venture");
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);
      return new Dag<string, decimal>(apex);
    }

    private static string[] Edges(IDagnumerable<string, decimal> dag) =>
      dag.GetEdges().Select(e => $"{e.Parent}->{e.Child}:{e.Edge}").ToArray();

    private static decimal Lookthrough(IDagnumerable<string, decimal> dag) =>
      dag.Sourcefix().Scan<decimal>(
           (node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(i => i.Value * i.Edge))
         .GetSinks()
         .Single()
         .Accumulate;

    [TestMethod]
    public void KeepEverything_IsTheIdentity()
    {
      var kept = Diamond().Where(node => true, (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, kept.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(Edges(Diamond()), Edges(kept));

      for (var ordinal = 0; ordinal < kept.Count; ordinal++)
        Assert.AreEqual(ordinal, kept.SourceOrdinal(ordinal));
    }

    [TestMethod]
    public void BypassOneMiddle_ComposesTheThroughEdge_AtTheFilteredSeat()
    {
      var bypassed = Diamond().Where(node => node != "left", (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "apex", "right", "venture" }, bypassed.GetTopologicalOrder().ToArray());

      // The through-edge appears at the filtered child's position in the parent's out-block
      // (the tree promotion's presentation): 60% × 70% = 42%.
      CollectionAssert.AreEqual(
        new[] { "apex->venture:0.4200", "apex->right:0.40", "right->venture:0.30" },
        Edges(bypassed));

      // Seats survive: apex(0), right(2), venture(3) in the original's ordinals.
      Assert.AreEqual(0, bypassed.SourceOrdinal(0));
      Assert.AreEqual(2, bypassed.SourceOrdinal(1));
      Assert.AreEqual(3, bypassed.SourceOrdinal(2));
    }

    [TestMethod]
    public void BypassBothMiddles_YieldsParallelEdges_AndPreservesLookthrough()
    {
      var bypassed = Diamond().Where(
        node => node == "apex" || node == "venture",
        (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "apex", "venture" }, bypassed.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "apex->venture:0.4200", "apex->venture:0.1200" },
        Edges(bypassed));

      // Dissolving pass-through entities changes presentation, never arithmetic.
      Assert.AreEqual(Lookthrough(Diamond()), Lookthrough(bypassed));
      Assert.AreEqual(0.54m, Lookthrough(bypassed));
    }

    [TestMethod]
    public void FilteredChain_ComposesLeftToRight()
    {
      // a -(w1)-> x -(w2)-> y -(w3)-> b with x and y filtered: one through-edge whose
      // payload is the left fold along the path -- ((w1 ∘ w2) ∘ w3), pinned with a
      // non-commutative composer.
      var a = new DagNode<string, string>("a");
      var x = a.AddChild("x", "w1");
      var y = x.AddChild("y", "w2");
      y.AddChild("b", "w3");
      var dag = new Dag<string, string>(a);

      var bypassed = dag.Where(
        node => node == "a" || node == "b",
        (inEdge, outEdge) => $"({inEdge}*{outEdge})");

      CollectionAssert.AreEqual(new[] { "a", "b" }, bypassed.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "a->b:((w1*w2)*w3)" },
        bypassed.GetEdges().Select(e => $"{e.Parent}->{e.Child}:{e.Edge}").ToArray());
    }

    [TestMethod]
    public void FilteredNode_ManufacturesInTimesOutEdges()
    {
      // p1, p2 -> x -> c1, c2 with x filtered: 2 × 2 through-edges, grouped by origin
      // parent, each parent's fan in x's out-edge order.
      var p1 = new DagNode<string, decimal>("p1");
      var p2 = new DagNode<string, decimal>("p2");
      var x = p1.AddChild("x", 2m);
      p2.AddChild(x, 3m);
      x.AddChild("c1", 5m);
      x.AddChild("c2", 7m);
      var dag = new Dag<string, decimal>(p1, p2);

      var bypassed = dag.Where(node => node != "x", (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(
        new[] { "p1->c1:10", "p1->c2:14", "p2->c1:15", "p2->c2:21" },
        Edges(bypassed));
    }

    [TestMethod]
    public void FilteredSource_PromotesItsChildrenToSources()
    {
      // The tree's filtered-root promotion, dag-side: bypass is not removal, so b survives
      // its only parent's filtering -- as a source. (Liveness would have killed it; Where
      // deliberately has no liveness.)
      var a = new DagNode<string, decimal>("a");
      var b = a.AddChild("b", 1m);
      b.AddChild("c", 2m);
      var dag = new Dag<string, decimal>(a);

      var bypassed = dag.Where(node => node != "a", (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "b", "c" }, bypassed.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "b" }, bypassed.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "b->c:2" }, Edges(bypassed));
    }

    [TestMethod]
    public void FilteredSink_JustVanishes()
    {
      // Bypassing a sink composes nothing (no out-edges to route through); the parent
      // keeps its other edges.
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m);
      a.AddChild("c", 2m);
      var dag = new Dag<string, decimal>(a);

      var bypassed = dag.Where(node => node != "c", (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "a", "b" }, bypassed.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "a->b:1" }, Edges(bypassed));
    }

    [TestMethod]
    public void EveryNodeConsultedExactlyOnce()
    {
      // Bypass severs nothing, so nothing is dead: the predicate runs once per node, in
      // topological order.
      var consulted = new List<string>();

      Diamond().Where(
        node =>
        {
          consulted.Add(node);
          return node == "apex" || node == "venture";
        },
        (inEdge, outEdge) => inEdge * outEdge);

      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, consulted);
    }
  }
}
