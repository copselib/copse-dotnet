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

      // The apex's lookthrough, attributed per route -- no double count, and each upflow names
      // its dispatcher (the child, in an upward pass). Arrival order is the pass's (reverse
      // topological: right completes before left).
      CollectionAssert.AreEqual(
        new[] { (("right", 0m), 120m, 0.40m), (("left", 0m), 420m, 0.60m) },
        byEntity["apex"].Inflows.Select(i => (i.Dispatcher, i.Value, i.Edge)).ToArray());
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

    // ---------------------------------------------------------------------------------------
    // SinkfixDispatchEdges -- the group-scoped edge writer.
    // ---------------------------------------------------------------------------------------

    // The GP shape: a sliver-owner source alongside the main fund, co-owning X; X owns Op.
    private static Dag<string, decimal> GpSliver()
    {
      var gp = new DagNode<string, decimal>("GP");
      var fund = new DagNode<string, decimal>("Fund");
      var x = new DagNode<string, decimal>("X");
      gp.AddChild(x, 0.002m);
      fund.AddChild(x, 0.998m);
      x.AddChild("Op", 1m);
      return new Dag<string, decimal>(gp, fund);
    }

    // Conditioning: zero the GP outcome, renormalize the survivors -- the caller's algebra,
    // one lambda, over the complete owner group.
    private static void ConditionOutGp(
      DagDispatchNode<string, decimal, decimal> entity,
      IReadOnlyList<DagDispatchTarget<string, decimal, decimal>> owners)
    {
      var gp = owners.Where(o => o.Value == "GP").Sum(o => o.Edge);
      foreach (var owner in owners)
        owner.Dispatch(owner.Value == "GP" ? 0m : owner.Edge / (1 - gp));
    }

    [TestMethod]
    public void SinkfixDispatchEdges_ConditionsTheOwnershipDistribution()
    {
      var conditioned = GpSliver().SinkfixDispatchEdges<string, decimal, decimal>(ConditionOutGp);

      // Edges rewritten in place: GP's stays, at zero, visible; the fund absorbs; nodes and
      // shape untouched; groups still sum to one.
      CollectionAssert.AreEquivalent(
        new[] { ("GP", "X", 0m), ("Fund", "X", 1m), ("X", "Op", 1m) },
        conditioned.GetEdges().Select(e => (e.Parent, e.Child, e.Edge)).ToList());

      // The distribution invariant survives conditioning: lookthrough is fully accounted.
      var lookthrough = conditioned.SourcefixScan<string, decimal, decimal>(
        (entity, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(i => i.Value * i.Edge));

      foreach (var node in lookthrough.GetTopologicalOrder())
        Assert.AreEqual(1m, node.Value);
    }

    [TestMethod]
    public void SinkfixDispatchEdges_MoneyFollowsTheConditionedEdges()
    {
      var moved = GpSliver()
        .SinkfixDispatchEdges<string, decimal, decimal>(ConditionOutGp)
        .SourcefixDispatch(1_000m, (node, targets) =>
        {
          var arrived = node.Inflows.Sum(i => i.Value);
          foreach (var target in targets)
            target.Dispatch(arrived * target.Edge);
        });

      var byEntity = moved.GetTopologicalOrder().ToDictionary(n => n.Value.Value, n => n.Value);

      CollectionAssert.AreEqual(
        new[] { ("GP", 0m), ("Fund", 1_000m) },
        byEntity["X"].Inflows.Select(i => (i.Dispatcher, i.Value)).ToArray(),
        "the GP edge is live and visibly zero; the fund's carries everything");
      Assert.AreEqual(1_000m, byEntity["Op"].Inflows.Sum(i => i.Value));
    }

    [TestMethod]
    public void SinkfixDispatchEdges_TheCascadeIsVisible_ChildrenResolveFirst()
    {
      // Reverse topological: Op's survey writes X->Op before X's survey runs, and X sees that
      // write among its out-edge results -- dispatcher, new payload, old payload.
      var seenAtX = new List<(string Dispatcher, decimal NewPayload, decimal OldPayload)>();

      GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((entity, owners) =>
      {
        if (entity.Value == "X")
          seenAtX.AddRange(entity.Inflows.Select(i => (i.Dispatcher, i.Value, i.Edge)));

        ConditionOutGp(entity, owners);
      });

      CollectionAssert.AreEqual(new[] { ("Op", 1m, 1m) }, seenAtX);
    }

    [TestMethod]
    public void SinkfixDispatchEdges_ParallelEdges_RewriteDistinctly_OrderPreserved()
    {
      var top = new DagNode<string, decimal>("top");
      var bottom = new DagNode<string, decimal>("bottom");
      top.AddChild(bottom, 0.25m);
      top.AddChild(bottom, 0.75m);

      var doubled = new Dag<string, decimal>(top)
        .SinkfixDispatchEdges<string, decimal, decimal>((entity, owners) =>
        {
          foreach (var owner in owners)
            owner.Dispatch(owner.Edge * 2);
        });

      CollectionAssert.AreEqual(
        new[] { ("top", "bottom", 0.50m, 0), ("top", "bottom", 1.50m, 1) },
        doubled.GetEdges().Select(e => (e.Parent, e.Child, e.Edge, e.InEdgeIndex)).ToArray(),
        "each parallel edge rewritten from its own slot, per-parent order preserved");
    }

    [TestMethod]
    public void SinkfixDispatchEdges_SurveysNonSourcesOnce_InReverseTopologicalOrder()
    {
      var surveyed = new List<string>();

      GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((entity, owners) =>
      {
        surveyed.Add(entity.Value);
        foreach (var owner in owners)
          owner.Dispatch(owner.Edge);
      });

      CollectionAssert.AreEqual(new[] { "Op", "X" }, surveyed,
        "sources have no in-edges and are never surveyed; every edge is written exactly once anyway");
    }

    [TestMethod]
    public void SinkfixDispatchEdges_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((entity, owners) => { }));
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
