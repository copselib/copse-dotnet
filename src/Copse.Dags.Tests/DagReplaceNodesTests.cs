using System;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // NODE REPLACEMENT (design-docs/SUBSTITUTION_TAXONOMY.md): every node becomes the graph
  // the selector returns. The load-bearing pins: the WIRING RULE (in-edges to the replacement's
  // sources, out-edges from EVERY replacement node -- the cell-division signature: shared
  // children gain edges, never copies); Keep occupies the original's seat (SourceOrdinal
  // carries) while multi-node replacements are wholly born-here; Drop follows the family's one
  // liveness rule -- PruneBefore is the all-Keep-or-Drop special case and their contents must
  // agree; a dead node's selector is never consulted.
  [TestClass]
  public class DagReplaceNodesTests
  {
    // The ownership diamond: apex owns left 60% / right 40%; each owns the venture (70%/30%).
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

    // a(b, c): one parent, two exclusive children -- the conversation's own division example.
    private static Dag<string, decimal> Fork()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m);
      a.AddChild("c", 2m);
      return new Dag<string, decimal>(a);
    }

    private static string[] Edges(IDagnumerable<string, decimal> dag) =>
      dag.GetEdges().Select(e => $"{e.Parent}->{e.Child}:{e.Edge}").ToArray();

    [TestMethod]
    public void KeepEverything_IsTheContentIdentity_SeatsIncluded()
    {
      var kept = Diamond().ReplaceNodes(DagNodeGraph<string, decimal>.Keep);

      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, kept.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(Edges(Diamond()), Edges(kept));

      // Seat preservation: single-node replacements occupy the originals' seats.
      for (var ordinal = 0; ordinal < kept.Count; ordinal++)
        Assert.AreEqual(ordinal, kept.SourceOrdinal(ordinal));
    }

    [TestMethod]
    public void Keep_WithARewrittenValue_IsSelectContent()
    {
      var byReplacement = Diamond().ReplaceNodes(node => DagNodeGraph<string, decimal>.Keep(node.ToUpperInvariant()));
      var byProjection = Diamond().Select(node => node.ToUpperInvariant());

      CollectionAssert.AreEqual(
        byProjection.GetTopologicalOrder().ToArray(),
        byReplacement.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void Split_DividesTheNode_SharedChildrenGainEdgesNeverCopies()
    {
      // The cell-division move: a(b, c) with a -> {a0, a1}. Both alternatives inherit both
      // out-edges; b and c appear ONCE -- sharing is edges, the tree's copies are its
      // unfolding (the taxonomy's whole point).
      var divided = Fork().ReplaceNodes(node =>
        node == "a"
          ? DagNodeGraph<string, decimal>.Split("a0", "a1")
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(new[] { "a0", "a1", "b", "c" }, divided.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "a0", "a1" }, divided.GetSources().ToArray());
      CollectionAssert.AreEqual(
        new[] { "a0->b:1", "a0->c:2", "a1->b:1", "a1->c:2" },
        Edges(divided));
    }

    [TestMethod]
    public void Split_InTheMiddle_FansBothSidesOfTheSeat()
    {
      // Dividing an interior node: every in-edge fans to every alternative (all are sources
      // of the replacement), every out-edge fans from every alternative.
      var divided = Diamond().ReplaceNodes(node =>
        node == "left"
          ? DagNodeGraph<string, decimal>.Split("l0", "l1")
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(new[] { "apex", "l0", "l1", "right", "venture" }, divided.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "apex->l0:0.60", "apex->l1:0.60", "apex->right:0.40", "l0->venture:0.70", "l1->venture:0.70", "right->venture:0.30" },
        Edges(divided));
    }

    [TestMethod]
    public void Chain_ShowsTheEveryNodeWiring()
    {
      // a -> Chain(p, q): out-edges leave from EVERY replacement node, so the skip edge
      // p->b appears beside the through path p->q->b -- the every-node row's signature,
      // and exactly what keeps deletion local (see the law battery).
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 5m);
      var dag = new Dag<string, decimal>(a);

      var expanded = dag.ReplaceNodes(node =>
        node == "a"
          ? DagNodeGraph<string, decimal>.Chain("p", (1m, "q"))
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(new[] { "p", "q", "b" }, expanded.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "p->q:1", "p->b:5", "q->b:5" },
        Edges(expanded));
    }

    [TestMethod]
    public void Graph_InEdgesLandOnSourcesOnly()
    {
      // p -> x(w), x replaced by a two-source funnel: the in-edge fans to the replacement's
      // SOURCES (s1, s2), never its interior (t).
      var p = new DagNode<string, decimal>("p");
      p.AddChild("x", 9m);
      var dag = new Dag<string, decimal>(p);

      var expanded = dag.ReplaceNodes(node =>
        node == "x"
          ? DagNodeGraph<string, decimal>.Graph(new[] { "s1", "s2", "t" }, (0, 2, 1m), (1, 2, 2m))
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(new[] { "p", "s1", "s2", "t" }, expanded.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "p->s1:9", "p->s2:9", "s1->t:1", "s2->t:2" },
        Edges(expanded));
    }

    [TestMethod]
    public void BornHere_MultiNodeReplacements_KeepNoSeat()
    {
      var divided = Diamond().ReplaceNodes(node =>
        node == "left"
          ? DagNodeGraph<string, decimal>.Split("l0", "l1")
          : DagNodeGraph<string, decimal>.Keep(node));

      // Result order: apex(0) l0(1) l1(2) right(3) venture(4); the alternatives are fresh.
      Assert.AreEqual(0, divided.SourceOrdinal(0));
      Assert.AreEqual(-1, divided.SourceOrdinal(1));
      Assert.AreEqual(-1, divided.SourceOrdinal(2));
      Assert.AreEqual(2, divided.SourceOrdinal(3));
      Assert.AreEqual(3, divided.SourceOrdinal(4));
    }

    [TestMethod]
    public void Drop_FollowsTheLivenessRule_PruneBeforeIsTheSpecialCase()
    {
      // Dropping one of the venture's parents spares it (another live path remains);
      // dropping both kills it. Contents must agree with PruneBefore exactly (prune
      // polarity: true = prune).
      var oneDropped = Diamond().ReplaceNodes(node =>
        node == "left" ? DagNodeGraph<string, decimal>.Drop : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(
        Diamond().PruneBefore(node => node == "left").GetTopologicalOrder().ToArray(),
        oneDropped.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "apex", "right", "venture" }, oneDropped.GetTopologicalOrder().ToArray());

      var bothDropped = Diamond().ReplaceNodes(node =>
        node == "left" || node == "right"
          ? DagNodeGraph<string, decimal>.Drop
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(new[] { "apex" }, bothDropped.GetTopologicalOrder().ToArray());
      Assert.AreEqual(0, Edges(bothDropped).Length);
    }

    [TestMethod]
    public void SelectorNeverSeesADeadNode()
    {
      // Dropping apex starves everything downstream; no other node is ever consulted, and
      // the result is the empty dag.
      var consulted = 0;

      var result = Diamond().ReplaceNodes(node =>
      {
        consulted++;
        Assert.AreEqual("apex", node, "a dead node's replacement is never consulted");
        return DagNodeGraph<string, decimal>.Drop;
      });

      Assert.AreEqual(1, consulted);
      Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GraphValidation_TheForwardEdgeRule_IsEnforced()
    {
      // The forward-edge rule is the load-bearing premise of "cycle-safe without
      // revalidation" -- every malformed shape must throw at authoring, not at rebuild.
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Graph(new[] { "a", "b" }, (1, 0, 1m)));   // backward
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Graph(new[] { "a", "b" }, (1, 1, 1m)));   // self
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Graph(new[] { "a", "b" }, (-1, 1, 1m)));  // below range
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Graph(new[] { "a", "b" }, (0, 2, 1m)));   // above range
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Graph(new string[0]));                    // no nodes
      Assert.ThrowsException<ArgumentException>(() =>
        DagNodeGraph<string, decimal>.Split());                                 // no alternatives
      Assert.ThrowsException<ArgumentNullException>(() =>
        DagNodeGraph<string, decimal>.Graph(null));
      Assert.ThrowsException<ArgumentNullException>(() =>
        DagNodeGraph<string, decimal>.Split(null));
      Assert.ThrowsException<ArgumentNullException>(() =>
        DagNodeGraph<string, decimal>.Chain("a", null));
    }

    [TestMethod]
    public void OperatorGuards_NullSeatsThrow()
    {
      Assert.ThrowsException<ArgumentNullException>(() => Diamond().ReplaceNodes(null));
      Assert.ThrowsException<ArgumentNullException>(() =>
        Diamond().ExpandNodesWhere(null, node => DagNodeGraph<string, decimal>.Keep(node)));
      Assert.ThrowsException<ArgumentNullException>(() =>
        Diamond().ExpandNodesWhere(node => true, null));
      Assert.ThrowsException<ArgumentNullException>(() =>
        Diamond().Where(null, (inEdge, outEdge) => inEdge));
      Assert.ThrowsException<ArgumentNullException>(() => Diamond().Where(node => true, null));
    }

    [TestMethod]
    public void ExpandNodesWhere_IsTheReplacementWithAKeepBranch()
    {
      var viaSugar = Diamond().ExpandNodesWhere(
        node => node == "left",
        node => DagNodeGraph<string, decimal>.Split("l0", "l1"));

      var viaReplacement = Diamond().ReplaceNodes(node =>
        node == "left"
          ? DagNodeGraph<string, decimal>.Split("l0", "l1")
          : DagNodeGraph<string, decimal>.Keep(node));

      CollectionAssert.AreEqual(viaReplacement.GetTopologicalOrder().ToArray(), viaSugar.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(Edges(viaReplacement), Edges(viaSugar));
    }
  }
}
