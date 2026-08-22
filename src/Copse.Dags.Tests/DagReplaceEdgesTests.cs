using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // EDGE REPLACEMENT: every edge becomes the path the selector returns. The
  // load-bearing pins: interior nodes are fresh and placed topologically (right after their
  // parent), with SourceOrdinal -1 (born here); Drop follows the family's liveness rule --
  // PruneEdges is the replacement's streaming special case and their contents must agree; Keep with
  // a rewritten payload is SelectEdges' content; pass-through subdivision preserves
  // lookthrough (the reify-the-missing-entity move changes presentation, never arithmetic).
  [TestClass]
  public class DagReplaceEdgesTests
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

    private static string[] Edges(IDagnumerable<string, decimal> dag) =>
      dag.GetEdges().Select(e => $"{e.Parent}->{e.Child}:{e.Edge}").ToArray();

    [TestMethod]
    public void KeepEverything_IsTheContentIdentity()
    {
      var kept = Diamond().ReplaceEdges(e => DagEdgePath<string, decimal>.Keep(e.Edge));

      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, kept.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(Edges(Diamond()), Edges(kept));
    }

    [TestMethod]
    public void Through_InterposesAFreshNode_PlacedAfterItsParent()
    {
      var expanded = Diamond().ReplaceEdges(e =>
        e.Parent == "left" && e.Child == "venture"
          ? DagEdgePath<string, decimal>.Through(e.Edge, "sip", 1m)
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      // The interior node sits immediately after its parent in the topological order.
      CollectionAssert.AreEqual(
        new[] { "apex", "left", "sip", "right", "venture" },
        expanded.GetTopologicalOrder().ToArray());

      CollectionAssert.AreEqual(
        new[] { "apex->left:0.60", "apex->right:0.40", "left->sip:0.70", "sip->venture:1", "right->venture:0.30" },
        Edges(expanded));
    }

    [TestMethod]
    public void Through_BornHereNodes_HaveNoSourceOrdinal()
    {
      var expanded = Diamond().ReplaceEdges(e =>
        e.Parent == "left" && e.Child == "venture"
          ? DagEdgePath<string, decimal>.Through(e.Edge, "sip", 1m)
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      // Result order: apex(0) left(1) sip(2) right(3) venture(4); originals correlate to the
      // capture's ordinals, the interior node is born here (-1).
      Assert.AreEqual(0, expanded.SourceOrdinal(0));
      Assert.AreEqual(1, expanded.SourceOrdinal(1));
      Assert.AreEqual(-1, expanded.SourceOrdinal(2));
      Assert.AreEqual(2, expanded.SourceOrdinal(3));
      Assert.AreEqual(3, expanded.SourceOrdinal(4));
    }

    [TestMethod]
    public void Drop_FollowsTheLivenessRule_PruneEdgesIsTheStreamingSpecialCase()
    {
      // Dropping one of the venture's in-edges spares it (another live path remains);
      // dropping both kills it. Both must agree with PruneEdges' content exactly.
      var oneDropped = Diamond().ReplaceEdges(e =>
        e.Parent == "left" && e.Child == "venture"
          ? DagEdgePath<string, decimal>.Drop
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      CollectionAssert.AreEqual(
        Diamond().PruneEdges(e => e.Parent == "left" && e.Child == "venture").GetTopologicalOrder().ToArray(),
        oneDropped.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        Edges(Diamond().PruneEdges(e => e.Parent == "left" && e.Child == "venture")),
        Edges(oneDropped));

      var bothDropped = Diamond().ReplaceEdges(e =>
        e.Child == "venture"
          ? DagEdgePath<string, decimal>.Drop
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      CollectionAssert.AreEqual(
        Diamond().PruneEdges(e => e.Child == "venture").GetTopologicalOrder().ToArray(),
        bothDropped.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "apex", "left", "right" }, bothDropped.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void Keep_WithARewrittenPayload_IsSelectEdgesContent()
    {
      var doubledByBind = Diamond().ReplaceEdges(e => DagEdgePath<string, decimal>.Keep(e.Edge * 2));
      var doubledByProjection = Diamond().SelectEdges(e => e.Edge * 2);

      CollectionAssert.AreEqual(Edges(doubledByProjection), Edges(doubledByBind));
    }

    [TestMethod]
    public void Chain_InterposesInOrder()
    {
      var chained = Diamond().ReplaceEdges(e =>
        e.Parent == "left" && e.Child == "venture"
          ? DagEdgePath<string, decimal>.Chain(
              e.Edge,
              new DagEdgePathLink<string, decimal>("first", 1m),
              new DagEdgePathLink<string, decimal>("second", 1m))
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      CollectionAssert.AreEqual(
        new[] { "apex", "left", "first", "second", "right", "venture" },
        chained.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(
        new[] { "apex->left:0.60", "apex->right:0.40", "left->first:0.70", "first->second:1", "second->venture:1", "right->venture:0.30" },
        Edges(chained));
    }

    [TestMethod]
    public void PassThroughSubdivision_PreservesLookthrough()
    {
      // The reify-the-missing-entity move: interposing with (stake, 1.0) changes the
      // presentation, never the arithmetic -- effective ownership at the venture is 54%
      // before and after.
      static decimal Lookthrough(IDagnumerable<string, decimal> dag) =>
        dag.SourcefixScan<string, decimal, decimal>(
             (node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(i => i.Value * i.Edge))
           .GetSinks()
           .Single()
           .Accumulate;

      var expanded = Diamond().ReplaceEdges(e =>
        e.Child == "venture"
          ? DagEdgePath<string, decimal>.Through(e.Edge, $"via-{e.Parent}", 1m)
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      Assert.AreEqual(Lookthrough(Diamond()), Lookthrough(expanded));
      Assert.AreEqual(0.54m, Lookthrough(expanded));
    }

    [TestMethod]
    public void ExpandEdgesWhere_IsTheBindWithAKeepBranch()
    {
      var viaSugar = Diamond().ExpandEdgesWhere(
        e => e.Child == "venture",
        e => DagEdgePath<string, decimal>.Through(e.Edge, $"via-{e.Parent}", 1m));

      var viaBind = Diamond().ReplaceEdges(e =>
        e.Child == "venture"
          ? DagEdgePath<string, decimal>.Through(e.Edge, $"via-{e.Parent}", 1m)
          : DagEdgePath<string, decimal>.Keep(e.Edge));

      CollectionAssert.AreEqual(viaBind.GetTopologicalOrder().ToArray(), viaSugar.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(Edges(viaBind), Edges(viaSugar));
    }

    [TestMethod]
    public void ExpandEdgesWhere_TheReifyMove_MakesAnchorsOfDecorations()
    {
      // The PoC's shape in miniature: interpose an anchor on every "program" edge, then a
      // restart-at-anchors upward scan attributes ownership per anchor -- the path-dependent
      // query becomes a per-node one. PLACEMENT MATTERS for attribution: the stake rides the
      // leg BELOW the anchor (the owner wholly owns its program position; the position owns
      // the stake of the target), so the anchor's own lookthrough IS the stake.
      var expanded = Diamond().ExpandEdgesWhere(
        e => e.Child == "venture",
        e => DagEdgePath<string, decimal>.Through(1m, $"program-{e.Parent}", e.Edge));

      CollectionAssert.AreEqual(
        new[] { "apex", "left", "program-left", "right", "program-right", "venture" },
        expanded.GetTopologicalOrder().ToArray());

      // Effective ownership per anchor: each program node's lookthrough of the venture.
      var lookthrough = expanded.SinkfixScan<string, decimal, decimal>(
        (node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(i => i.Value * i.Edge));

      CollectionAssert.AreEqual(
        new[] { ("program-left", 0.70m), ("program-right", 0.30m) },
        lookthrough.Values
          .Where(pairing => pairing.Node.StartsWith("program-"))
          .Select(pairing => (pairing.Node, pairing.Accumulate))
          .ToArray());
    }

    [TestMethod]
    public void SelectorNeverSeesADeadParentsEdges()
    {
      // Dropping apex->left kills left (its only in-edge); left's own out-edge must never be
      // consulted -- and the venture survives through right alone.
      var consulted = 0;

      var result = Diamond().ReplaceEdges(e =>
      {
        Assert.AreNotEqual("left", e.Parent, "a dead parent's edges are never consulted");
        consulted++;
        return e.Parent == "apex" && e.Child == "left"
          ? DagEdgePath<string, decimal>.Drop
          : DagEdgePath<string, decimal>.Keep(e.Edge);
      });

      Assert.AreEqual(3, consulted); // apex->left, apex->right, right->venture
      CollectionAssert.AreEqual(new[] { "apex", "right", "venture" }, result.GetTopologicalOrder().ToArray());
    }
  }
}
