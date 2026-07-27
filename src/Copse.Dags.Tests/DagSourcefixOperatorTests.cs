using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The downward money operators over the contract (docs/DAG_CONTRACT_DESIGN.md, phase 2b):
  // SourcefixScan (edge-paired inflows -- effective-ownership lookthrough is the headline) and
  // SourcefixDispatch (the survey-shaped allocation pass; exactly-once slots; live edges only,
  // so pruning blockers upstream composes into the allocation). Differentials against the
  // builder's own SourcefixScan close the loop on the oracle.
  [TestClass]
  public class DagSourcefixOperatorTests
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

    // Effective ownership: a source owns itself outright; below, sum each inflow's owner
    // fraction times the edge it rides -- the sum-over-paths-of-products, computed in one pass.
    private static decimal EffectiveOwnership(string entity, IReadOnlyList<DagInflow<decimal, decimal>> inflows)
      => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge);

    // ---------------------------------------------------------------------------------------
    // SourcefixScan.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SourcefixScan_ComputesEffectiveOwnership_ThroughTheDiamond()
    {
      var ownership = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      CollectionAssert.AreEqual(
        new[] { 1m, 0.60m, 0.40m, 0.54m },
        ownership.GetTopologicalOrder().Select(n => n.Value).ToArray(),
        "the venture's lookthrough: 60% x 70% + 40% x 30%");
    }

    [TestMethod]
    public void SourcefixScan_PreservesShapeAndEdges()
    {
      var scanned = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);
      var order = scanned.GetTopologicalOrder();

      Assert.AreEqual(1, scanned.Sources.Count);
      Assert.AreEqual(4, order.Count);

      var edges = order
        .SelectMany(n => n.ChildEdges.Select(e => (Parent: n.Value, Child: e.Child.Value, Edge: e.Value)))
        .OrderBy(edge => edge).ToList();

      CollectionAssert.AreEqual(
        new List<(decimal, decimal, decimal)>
        {
          (0.40m, 0.54m, 0.30m),  // right -> venture, 30%
          (0.60m, 0.54m, 0.70m),  // left -> venture, 70%
          (1m, 0.60m, 0.60m),     // apex -> left, 60%
          (1m, 0.40m, 0.40m),     // apex -> right, 40%
        }.OrderBy(edge => edge).ToList(),
        edges,
        "shape-isomorphic: payloads carried, sharing preserved (the venture node appears once)");
    }

    [TestMethod]
    public void SourcefixScan_ComposesWithAnUpstreamPrune()
    {
      // The blocker composition: with left pruned, the venture's only inflow rides right.
      var ownership = Diamond()
        .PruneBefore(entity => entity == "left")
        .SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      CollectionAssert.AreEqual(
        new[] { 1m, 0.40m, 0.12m },
        ownership.GetTopologicalOrder().Select(n => n.Value).ToArray(),
        "venture = 40% x 30%: the pruned path contributes nothing");
    }

    [TestMethod]
    public void SourcefixScan_MatchesTheBuilderOracle()
    {
      // Same accumulation both sides (the oracle's inflows are bare; the contract's carry
      // edges -- align by summing the same product via the node's parent edges).
      var contract = Diamond()
        .SourcefixScan<string, decimal, decimal>(EffectiveOwnership)
        .GetTopologicalOrder().Select(n => n.Value).ToList();

      var oracle = Diamond()
        .OracleSourcefixScan<string, decimal, decimal>((node, inflows) =>
          inflows.Count == 0
            ? 1m
            : inflows.Select((inflow, index) => inflow * node.ParentEdges[index].Value).Sum())
        .GetTopologicalOrder().Select(n => n.Value).ToList();

      CollectionAssert.AreEqual(oracle, contract);
    }

    [TestMethod]
    public void SourcefixScan_ResultIsComposite_BothDimensionsAfford()
    {
      var scanned = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      // The materialization is an upgrade: forward AND backward walks both serve.
      using var forward = scanned.GetForwardDagnumerator();
      using var backward = scanned.GetBackwardDagnumerator();

      Assert.IsTrue(forward.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.IsTrue(backward.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(1m, forward.Node, "forward sources at the apex");
      Assert.AreEqual(0.54m, backward.Node, "backward sources at the venture");
    }

    // ---------------------------------------------------------------------------------------
    // SourcefixDispatch.
    // ---------------------------------------------------------------------------------------

    // The pro-rata survey: each node forwards its whole arrival, split by edge fraction.
    private static void ProRata(
      DagDispatchNode<string, decimal, decimal> node,
      IReadOnlyList<DagDispatchTarget<string, decimal, decimal>> targets)
    {
      var arrived = node.Inflows.Sum(inflow => inflow.Value);
      foreach (var target in targets)
        target.Dispatch(arrived * target.Edge);
    }

    [TestMethod]
    public void SourcefixDispatch_MovesMoneyThroughTheDiamond()
    {
      var moved = Diamond().SourcefixDispatch(1000m, ProRata);

      var byEntity = moved.GetTopologicalOrder().ToDictionary(n => n.Value.Value, n => n.Value);

      CollectionAssert.AreEqual(new[] { 1000m }, byEntity["apex"].Inflows.Select(i => i.Value).ToArray());
      CollectionAssert.AreEqual(new[] { 600m }, byEntity["left"].Inflows.Select(i => i.Value).ToArray());
      CollectionAssert.AreEqual(new[] { 400m }, byEntity["right"].Inflows.Select(i => i.Value).ToArray());
      CollectionAssert.AreEqual(
        new[] { (420m, 0.70m), (120m, 0.30m) },
        byEntity["venture"].Inflows.Select(i => (i.Value, i.Edge)).ToArray(),
        "attribution survives: the venture knows what arrived on which edge");
    }

    [TestMethod]
    public void SourcefixDispatch_PruningBlockersComposesIntoTheAllocation()
    {
      // The MoveMoney shape: blockers pruned first, so their edges are never surveyed and
      // nothing is allocated toward them.
      var moved = Diamond()
        .PruneBefore(entity => entity == "left")
        .SourcefixDispatch(1000m, ProRata);

      var byEntity = moved.GetTopologicalOrder().ToDictionary(n => n.Value.Value, n => n.Value);

      Assert.AreEqual(3, byEntity.Count, "left is gone");
      CollectionAssert.AreEqual(
        new[] { (120m, 0.30m) },
        byEntity["venture"].Inflows.Select(i => (i.Value, i.Edge)).ToArray(),
        "only right's edge was live to fund: 1000 x 40% x 30%");
    }

    [TestMethod]
    public void SourcefixDispatch_SelectsIntoAReceivedView()
    {
      // The decorate-then-choose composition: the full pipeline down to plain received totals.
      var received = Diamond()
        .SourcefixDispatch(1000m, ProRata)
        .Select(dispatchNode => (Entity: dispatchNode.Value, Received: dispatchNode.Inflows.Sum(i => i.Value)));

      var entries = new List<(string, decimal)>();
      using var walk = received.GetForwardDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.EnteringNode)
          entries.Add(walk.Node);

      CollectionAssert.AreEqual(
        new List<(string, decimal)> { ("apex", 1000m), ("left", 600m), ("right", 400m), ("venture", 540m) },
        entries);
    }

    [TestMethod]
    public void SourcefixDispatch_SurveysLeavesNever_AndEveryOtherNodeOnce()
    {
      var surveyed = new List<string>();

      Diamond().SourcefixDispatch(1m, (node, targets) =>
      {
        surveyed.Add(node.Value);
        foreach (var target in targets)
          target.Dispatch(0m);
      });

      CollectionAssert.AreEqual(new[] { "apex", "left", "right" }, surveyed,
        "the venture has no live out-edges; nothing to survey");
    }

    [TestMethod]
    public void SourcefixDispatch_IsRoot_TrueOnlyForSources()
    {
      var moved = Diamond().SourcefixDispatch(1000m, ProRata);
      var byEntity = moved.GetTopologicalOrder().ToDictionary(n => n.Value.Value, n => n.Value);

      Assert.IsTrue(byEntity["apex"].IsSource);
      Assert.IsFalse(byEntity["left"].IsSource);
      Assert.IsFalse(byEntity["right"].IsSource);
      Assert.IsFalse(byEntity["venture"].IsSource);
    }

    [TestMethod]
    public void GetEdges_YieldsEveryEdgeOnce_WithBothEndpoints()
    {
      // The "one artifact per relationship" projection: each edge with parent, child, payload,
      // and its index among the child's in-edges (arrival order).
      CollectionAssert.AreEqual(
        new[]
        {
          ("apex", "left", 0.60m, 0),
          ("apex", "right", 0.40m, 0),
          ("left", "venture", 0.70m, 0),
          ("right", "venture", 0.30m, 1),
        },
        Diamond().GetEdges().Select(e => (e.Parent, e.Child, e.Edge, e.InEdgeIndex)).ToArray());
    }

    [TestMethod]
    public void GetEdges_OverADispatchResult_BuildsTransfers_InflowsIndexAligned()
    {
      // The work-integration pattern: one transfer per edge, the amount recovered from the
      // child's inflows by IN-EDGE INDEX -- never by payload comparison (user values are never
      // compared; parallel edges stay unambiguous).
      var transfers = Diamond()
        .SourcefixDispatch(1000m, ProRata)
        .GetEdges()
        .Select(edge => (
          From: edge.Parent.Value,
          To: edge.Child.Value,
          Fraction: edge.Edge,
          Amount: edge.Child.Inflows[edge.InEdgeIndex].Value))
        .ToArray();

      CollectionAssert.AreEqual(
        new[]
        {
          ("apex", "left", 0.60m, 600m),
          ("apex", "right", 0.40m, 400m),
          ("left", "venture", 0.70m, 420m),
          ("right", "venture", 0.30m, 120m),
        },
        transfers,
        "every contributed cent appears as exactly one transfer per relationship");
    }

    [TestMethod]
    public void SourcefixDispatch_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        Diamond().SourcefixDispatch(1m, (node, targets) =>
        {
          foreach (var target in targets.Skip(1))
            target.Dispatch(0m);
        }));
    }

    [TestMethod]
    public void SourcefixDispatch_ADoubleDispatchThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        Diamond().SourcefixDispatch(1m, (node, targets) =>
        {
          foreach (var target in targets)
            target.Dispatch(0m);
          targets[0].Dispatch(0m);
        }));
    }
  }
}
