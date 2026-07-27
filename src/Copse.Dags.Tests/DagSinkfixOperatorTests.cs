using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The upward operators (docs/DAG_CONTRACT_DESIGN.md, phase 3): SinkfixScan (per-use
  // roll-ups; the shared child appears in each parent's list -- the documented diamond
  // choice) and SinkfixDispatch -- the attribution dual that closes the deferred
  // upward-diamond semantic: each node decides what travels up each in-edge, so shared
  // subtrees are never double-counted, and the two directions agree: the ownership the
  // downward scan computes is exactly the attribution the upward dispatch delivers.
  [TestClass]
  public class DagSinkfixOperatorTests
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

    // The valued diamond: same shape, the venture holding 1000, everyone else nothing.
    private static Dag<(string Name, decimal Holding), decimal> ValuedDiamond()
    {
      var apex = new DagNode<(string, decimal), decimal>(("apex", 0m));
      var left = apex.AddChild(("left", 0m), 0.60m);
      var right = apex.AddChild(("right", 0m), 0.40m);
      var venture = new DagNode<(string, decimal), decimal>(("venture", 1000m));
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);
      return new Dag<(string, decimal), decimal>(apex);
    }

    // ---------------------------------------------------------------------------------------
    // SinkfixScan.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SinkfixScan_NodeCount_DoubleCountsTheSharedChild_ByDocumentedChoice()
    {
      // Per-use semantics: the venture's (single, memoized) result rides up BOTH edges, so a
      // naive roll-up counts it twice -- 1 + 2 + 2 = 5 over four nodes. That is the documented
      // caller's choice; SinkfixDispatch is the anti-double-count tool.
      var counts = Diamond().SinkfixScan<string, int, decimal>(
        (node, childResults) => 1 + childResults.Sum(child => child.Value));

      CollectionAssert.AreEqual(
        new[] { 5, 2, 2, 1 },
        counts.GetTopologicalOrder().Select(n => n.Value).ToArray());
    }

    [TestMethod]
    public void SinkfixScan_EachNodeComputedOnce_SharedOrNot()
    {
      var computed = new List<string>();

      Diamond().SinkfixScan<string, int, decimal>((node, childResults) =>
      {
        computed.Add(node);
        return 0;
      });

      CollectionAssert.AreEqual(new[] { "venture", "right", "left", "apex" }, computed,
        "reverse topological order, the venture once despite two parents");
    }

    [TestMethod]
    public void SinkfixScan_PreservesShapeAndEdges()
    {
      var scanned = Diamond().SinkfixScan<string, string, decimal>((node, _) => node.ToUpperInvariant());
      var order = scanned.GetTopologicalOrder();

      Assert.AreEqual(1, scanned.Sources.Count);
      Assert.AreEqual("APEX", scanned.Sources[0].Value);

      var edges = order
        .SelectMany(n => n.ChildEdges.Select(e => (Parent: n.Value, Child: e.Child.Value, Edge: e.Value)))
        .OrderBy(edge => edge).ToList();

      CollectionAssert.AreEqual(
        new List<(string, string, decimal)>
        {
          ("APEX", "LEFT", 0.60m),
          ("APEX", "RIGHT", 0.40m),
          ("LEFT", "VENTURE", 0.70m),
          ("RIGHT", "VENTURE", 0.30m),
        }.OrderBy(edge => edge).ToList(),
        edges,
        "shape-isomorphic: the venture appears once, payloads carried, per-parent order kept");
    }

    [TestMethod]
    public void SinkfixScan_MatchesTheBuilderOracle()
    {
      var contract = Diamond()
        .SinkfixScan<string, int, decimal>((node, childResults) => 1 + childResults.Sum(c => c.Value))
        .GetTopologicalOrder().Select(n => n.Value).ToList();

      var oracle = Diamond()
        .OracleSinkfixScan<string, decimal, int>((node, childResults) => 1 + childResults.Sum())
        .GetTopologicalOrder().Select(n => n.Value).ToList();

      CollectionAssert.AreEqual(oracle, contract);
    }

    [TestMethod]
    public void SinkfixScan_ComposesWithAnUpstreamPrune()
    {
      var counts = Diamond()
        .PruneBefore(entity => entity == "left")
        .SinkfixScan<string, int, decimal>((node, childResults) => 1 + childResults.Sum(c => c.Value));

      CollectionAssert.AreEqual(
        new[] { 3, 2, 1 },
        counts.GetTopologicalOrder().Select(n => n.Value).ToArray(),
        "apex + right + venture, one path each");
    }

    // ---------------------------------------------------------------------------------------
    // SinkfixDispatch -- the upward-diamond attribution.
    // ---------------------------------------------------------------------------------------

    // The attribution survey: a node's total (own holding + what its children sent it) travels
    // up each in-edge scaled by that edge's ownership fraction.
    private static void AttributeUp(
      DagDispatchNode<(string Name, decimal Holding), decimal, decimal> node,
      IReadOnlyList<DagDispatchTarget<(string Name, decimal Holding), decimal, decimal>> targets)
    {
      var total = node.Value.Holding + node.Inflows.Sum(upflow => upflow.Value);
      foreach (var target in targets)
        target.Dispatch(total * target.Edge);
    }

    [TestMethod]
    public void SinkfixDispatch_AttributesTheVentureToItsUltimateOwner()
    {
      var attributed = ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);

      var byEntity = attributed.GetTopologicalOrder().ToDictionary(n => n.Value.Value.Name, n => n.Value);

      // The venture holds 1000 and originates everything (no upflows -- sinks receive none).
      Assert.AreEqual(0, byEntity["venture"].Inflows.Count);

      // Each middle entity sees its edge's share of the venture.
      CollectionAssert.AreEqual(new[] { (700m, 0.70m) }, byEntity["left"].Inflows.Select(i => (i.Value, i.Edge)).ToArray());
      CollectionAssert.AreEqual(new[] { (300m, 0.30m) }, byEntity["right"].Inflows.Select(i => (i.Value, i.Edge)).ToArray());

      // The apex's lookthrough, attributed per route -- no double count. Arrival order is the
      // pass's (reverse topological: right completes before left).
      CollectionAssert.AreEqual(
        new[] { (120m, 0.40m), (420m, 0.60m) },
        byEntity["apex"].Inflows.Select(i => (i.Value, i.Edge)).ToArray());
      Assert.AreEqual(540m, byEntity["apex"].Inflows.Sum(i => i.Value));
    }

    [TestMethod]
    public void TheTwoDirectionsAgree_OwnershipDownEqualsAttributionUp()
    {
      // The duality that closes the deferred diamond semantic: the effective-ownership
      // fraction the DOWNWARD scan computes, times the holding, equals the attribution the
      // UPWARD dispatch delivers to the apex.
      var ownershipDown = Diamond()
        .SourcefixScan<string, decimal, decimal>((entity, inflows) =>
          inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge))
        .GetTopologicalOrder().Last().Value;

      var attributedUp = ValuedDiamond()
        .SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp)
        .GetTopologicalOrder()
        .Single(n => n.Value.Value.Name == "apex")
        .Value.Inflows.Sum(i => i.Value);

      Assert.AreEqual(ownershipDown * 1000m, attributedUp, "54% of 1000, both ways");
    }

    [TestMethod]
    public void SinkfixDispatch_SurveysSourcesNever_AndEveryOtherNodeOnce()
    {
      var surveyed = new List<string>();

      ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>((node, targets) =>
      {
        surveyed.Add(node.Value.Name);
        foreach (var target in targets)
          target.Dispatch(0m);
      });

      CollectionAssert.AreEqual(new[] { "venture", "right", "left" }, surveyed,
        "the apex has no in-edges; its resolved inflows ARE the result");
    }

    [TestMethod]
    public void SinkfixDispatch_IsRoot_TrueOnlyForSources()
    {
      var attributed = ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);
      var byEntity = attributed.GetTopologicalOrder().ToDictionary(n => n.Value.Value.Name, n => n.Value);

      Assert.IsTrue(byEntity["apex"].IsSource);
      Assert.IsFalse(byEntity["left"].IsSource);
      Assert.IsFalse(byEntity["right"].IsSource);
      Assert.IsFalse(byEntity["venture"].IsSource);
    }

    [TestMethod]
    public void SinkfixDispatch_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>((node, targets) => { }));
    }

    [TestMethod]
    public void SinkfixDispatch_ComposesWithAnUpstreamPrune()
    {
      // Blocker on the left: only the right route attributes upward.
      var attributed = ValuedDiamond()
        .PruneBefore(entity => entity.Name == "left")
        .SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);

      var apex = attributed.GetTopologicalOrder().Single(n => n.Value.Value.Name == "apex").Value;

      CollectionAssert.AreEqual(
        new[] { (120m, 0.40m) },
        apex.Inflows.Select(i => (i.Value, i.Edge)).ToArray(),
        "1000 x 30% x 40%, the surviving route only");
    }
  }
}
