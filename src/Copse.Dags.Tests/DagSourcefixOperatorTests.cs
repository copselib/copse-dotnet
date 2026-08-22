using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The downward money operators over the contract (design-docs/DAG_CONTRACT_DESIGN.md, phase 2b):
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

    // Source-ness is a STREAM fact, not a decoration: a discovery with no dispatching parent is
    // the conventional source discovery (the result buffers carry no Sources list -- the builder's
    // node-set view does not survive the re-founding).
    private static List<TNode> Sources<TNode, TEdge>(IDagnumerable<TNode, TEdge> source)
    {
      var sources = new List<TNode>();

      using var walk = source.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.DiscoveringNode && walk.ParentOrdinal < 0)
          sources.Add(walk.Node);

      return sources;
    }

    // The post-pass arrival read, edge context included. Provenance no longer travels ON the
    // result (the split-homes ruling, 2026-08-05): "who wrote arrival i of node n" is the
    // GetEdges join on in-edge index -- never a payload comparison, so parallel edges stay
    // unambiguous.
    private static (string From, decimal Amount, decimal Edge)[] ArrivalsAt(
      DagBuffer<DagDispatchResult<string, decimal>, decimal> dispatched, string entity)
      => dispatched.GetEdges()
        .Where(edge => edge.Child.Node == entity)
        .Select(edge => (edge.Parent.Node, edge.Child.Arrivals[edge.InEdgeIndex], edge.Edge))
        .ToArray();

    // ---------------------------------------------------------------------------------------
    // SourcefixScan.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SourcefixScan_ComputesEffectiveOwnership_ThroughTheDiamond()
    {
      var ownership = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      CollectionAssert.AreEqual(
        new[] { 1m, 0.60m, 0.40m, 0.54m },
        ownership.Values.Select(pairing => pairing.Accumulate).ToArray(),
        "the venture's lookthrough: 60% x 70% + 40% x 30%");
    }

    [TestMethod]
    public void SourcefixScan_PreservesShapeAndEdges()
    {
      var scanned = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      Assert.AreEqual(1, Sources(scanned).Count);
      Assert.AreEqual(4, scanned.Count);

      // The result is a PAIRING over the source's shared structure, so the edges read off the
      // buffer directly; project .Accumulate for the fold's values.
      var edges = scanned.GetEdges()
        .Select(e => (Parent: e.Parent.Accumulate, Child: e.Child.Accumulate, Edge: e.Edge))
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
        ownership.Values.Select(pairing => pairing.Accumulate).ToArray(),
        "venture = 40% x 30%: the pruned path contributes nothing");
    }

    [TestMethod]
    public void SourcefixScan_MatchesTheBuilderOracle()
    {
      // Same accumulation both sides (the oracle's inflows are bare; the contract's carry
      // edges -- align by summing the same product via the node's parent edges).
      var contract = Diamond()
        .SourcefixScan<string, decimal, decimal>(EffectiveOwnership)
        .Values.Select(pairing => pairing.Accumulate).ToList();

      var oracle = Diamond()
        .OracleSourcefixScan<string, decimal, decimal>((node, inflows) =>
          inflows.Count == 0
            ? 1m
            : inflows.Select((inflow, index) => inflow * node.ParentEdges[index].Value).Sum())
        .GetTopologicalOrder().Select(n => n.Value).ToList();

      CollectionAssert.AreEqual(oracle, contract);
    }

    [TestMethod]
    public void SourcefixScan_ResultIsACapture_BothOrientationsAfford()
    {
      var scanned = Diamond().SourcefixScan<string, decimal, decimal>(EffectiveOwnership);

      // The result is a capture, so the orientation flip is free: Transpose() is a swap of which
      // adjacency the walk reads, not a second dimension to have been afforded up front.
      using var forward = scanned.GetDagnumerator();
      using var backward = scanned.Transpose().GetDagnumerator();

      Assert.IsTrue(forward.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.IsTrue(backward.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(1m, forward.Node.Accumulate, "forward sources at the apex");
      Assert.AreEqual(0.54m, backward.Node.Accumulate, "the transpose sources at the venture");
    }

    // ---------------------------------------------------------------------------------------
    // SourcefixDispatch.
    // ---------------------------------------------------------------------------------------

    // The pro-rata survey: each node forwards its whole arrival, split by edge fraction.
    private static void ProRata(
      string subject,
      IReadOnlyList<DagDispatchInflow<string, decimal, decimal>> arrivals,
      IReadOnlyList<DagDispatchTarget<string, decimal, decimal>> targets)
    {
      var arrived = arrivals.Sum(arrival => arrival.Value);

      // The virtual source family, surveyed first (full participation, 2026-08-05): its single
      // dispatcher-less arrival IS the seed and its targets are the sources, carrying no payload
      // -- so the seed reaches each source verbatim, exactly the pre-re-founding semantics.
      if (subject is null)
      {
        foreach (var target in targets)
          target.Dispatch(arrived);
        return;
      }

      foreach (var target in targets)
        target.Dispatch(arrived * target.Edge);
    }

    [TestMethod]
    public void SourcefixDispatch_MovesMoneyThroughTheDiamond()
    {
      var moved = Diamond().SourcefixDispatch(1000m, ProRata);

      var byEntity = moved.Values.ToDictionary(result => result.Node, result => result);

      CollectionAssert.AreEqual(new[] { 1000m }, byEntity["apex"].Arrivals.ToArray());
      CollectionAssert.AreEqual(new[] { 600m }, byEntity["left"].Arrivals.ToArray());
      CollectionAssert.AreEqual(new[] { 400m }, byEntity["right"].Arrivals.ToArray());
      CollectionAssert.AreEqual(
        new[] { ("left", 420m, 0.70m), ("right", 120m, 0.30m) },
        ArrivalsAt(moved, "venture"),
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

      Assert.AreEqual(3, moved.Count, "left is gone");
      CollectionAssert.AreEqual(
        new[] { ("right", 120m, 0.30m) },
        ArrivalsAt(moved, "venture"),
        "only right's edge was live to fund: 1000 x 40% x 30%");
    }

    [TestMethod]
    public void SourcefixDispatch_SelectsIntoAReceivedView()
    {
      // The decorate-then-choose composition: the full pipeline down to plain received totals.
      var received = Diamond()
        .SourcefixDispatch(1000m, ProRata)
        .Select(result => (Entity: result.Node, Received: result.Arrivals.ToArray().Sum()));

      var entries = new List<(string, decimal)>();
      using var walk = received.GetDagnumerator();
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

      Diamond().SourcefixDispatch(1m, (subject, arrivals, targets) =>
      {
        surveyed.Add(subject);
        foreach (var target in targets)
          target.Dispatch(0m);
      });

      CollectionAssert.AreEqual(new[] { null, "apex", "left", "right" }, surveyed,
        "the virtual source family goes first (subject default); the venture has no live " +
        "out-edges, so nothing to survey");
    }

    [TestMethod]
    public void SourcefixDispatch_SourceNess_IsTheConventionalDiscovery()
    {
      // The retired IsSource decoration's replacement: source-ness is a stream fact, read off
      // the result buffer's own walk -- and it is exactly the node set the virtual family funds.
      var moved = Diamond().SourcefixDispatch(1000m, ProRata);

      CollectionAssert.AreEqual(
        new[] { "apex" },
        Sources(moved).Select(result => result.Node).ToArray());
    }

    [TestMethod]
    public void SourcefixDispatch_ArrivalsCorrelateToTheirDispatcher_ByInEdgeIndex()
    {
      // Provenance from the API, never smuggled in the payload -- but it does not TRAVEL on the
      // result (the split-homes ruling, 2026-08-05): who wrote arrival i is the GetEdges join.
      // The apex's lone arrival is the virtual family's, so no edge names it: authored outside.
      var moved = Diamond().SourcefixDispatch(1000m, ProRata);

      CollectionAssert.AreEqual(
        new[] { 1000m },
        moved.Values.Single(result => result.Node == "apex").Arrivals.ToArray());
      Assert.AreEqual(0, ArrivalsAt(moved, "apex").Length, "the seed arrives from outside the dag");

      CollectionAssert.AreEqual(new[] { ("apex", 600m, 0.60m) }, ArrivalsAt(moved, "left"));
      CollectionAssert.AreEqual(
        new[] { ("left", 420m, 0.70m), ("right", 120m, 0.30m) },
        ArrivalsAt(moved, "venture"),
        "the venture knows who funded it, per edge");
    }

    [TestMethod]
    public void SourcefixDispatch_TheSurveyCanCorrelateInflowsByDispatcher_InCallback()
    {
      // The callback view is the Dispatcher's ONE home: the survey reads WHO sent each arrival
      // straight off the API. The virtual family leads -- default subject, one dispatcher-less
      // arrival carrying the seed -- which is the in-band arrived-from-outside test.
      var arrivals = new List<(string At, string From, decimal Amount)>();

      Diamond().SourcefixDispatch(1000m, (subject, inflows, targets) =>
      {
        foreach (var inflow in inflows)
          arrivals.Add((subject, inflow.Dispatcher, inflow.Value));

        ProRata(subject, inflows, targets);
      });

      CollectionAssert.AreEqual(
        new[]
        {
          ((string)null, (string)null, 1000m),
          ("apex", null, 1000m),
          ("left", "apex", 600m),
          ("right", "apex", 400m),
        },
        arrivals);
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
          From: edge.Parent.Node,
          To: edge.Child.Node,
          Fraction: edge.Edge,
          Amount: edge.Child.Arrivals[edge.InEdgeIndex]))
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

    // ---------------------------------------------------------------------------------------
    // SourcefixDispatchEdges -- the downward group-scoped edge writer.
    // ---------------------------------------------------------------------------------------

    // Path-cumulative ownership carried TO each edge: a source owns itself outright; below,
    // each out-edge's new payload = (sum of the node's rewritten in-edge payloads) x its old
    // fraction -- the cascade doing sum-over-paths-of-products, landing ON the edges.
    private static void CumulativeOwnership(
      string subject,
      IReadOnlyList<DagDispatchInflow<string, decimal, decimal>> arrivals,
      IReadOnlyList<DagDispatchTarget<string, decimal, decimal>> targets)
    {
      // No virtual family here: an edge writer has no virtual edges to rewrite, so a source is
      // simply the node with no arrivals -- it owns itself outright.
      var carried = arrivals.Count == 0 ? 1m : arrivals.Sum(arrival => arrival.Value);
      foreach (var target in targets)
        target.Dispatch(carried * target.Edge);
    }

    [TestMethod]
    public void SourcefixDispatchEdges_WritesPathCumulativeOwnershipOntoEdges()
    {
      var cumulative = Diamond().SourcefixDispatchEdges<string, decimal, decimal>(CumulativeOwnership);

      CollectionAssert.AreEqual(
        new[]
        {
          ("apex", "left", 0.60m),
          ("apex", "right", 0.40m),
          ("left", "venture", 0.42m),   // 0.60 x 0.70
          ("right", "venture", 0.12m),  // 0.40 x 0.30
        },
        cumulative.GetEdges().Select(e => (e.Parent, e.Child, e.Edge.Accumulate)).ToArray(),
        "the venture's in-edges now carry effective ownership per route -- summing to the 54%");
    }

    [TestMethod]
    public void SourcefixDispatchEdges_TheCascadeIsVisible_AncestorsResolveFirst()
    {
      var seenAtVentureParents = new List<(string At, string Dispatcher, decimal NewPayload)>();

      Diamond().SourcefixDispatchEdges<string, decimal, decimal>((subject, arrivals, targets) =>
      {
        foreach (var inflow in arrivals)
          seenAtVentureParents.Add((subject, inflow.Dispatcher, inflow.Value));

        CumulativeOwnership(subject, arrivals, targets);
      });

      CollectionAssert.AreEqual(
        new[] { ("left", "apex", 0.60m), ("right", "apex", 0.40m) },
        seenAtVentureParents,
        "surveyed nodes see their in-edges' rewritten payloads; the venture is a sink, never surveyed");
    }

    [TestMethod]
    public void SourcefixDispatchEdges_SurveysNonSinksOnce_InTopologicalOrder()
    {
      var surveyed = new List<string>();

      Diamond().SourcefixDispatchEdges<string, decimal, decimal>((subject, arrivals, targets) =>
      {
        surveyed.Add(subject);
        foreach (var target in targets)
          target.Dispatch(target.Edge);
      });

      CollectionAssert.AreEqual(new[] { "apex", "left", "right" }, surveyed);
    }

    [TestMethod]
    public void SourcefixDispatchEdges_ParallelEdges_RewriteDistinctly_OrderPreserved()
    {
      var top = new DagNode<string, decimal>("top");
      var bottom = new DagNode<string, decimal>("bottom");
      top.AddChild(bottom, 0.25m);
      top.AddChild(bottom, 0.75m);

      var doubled = new Dag<string, decimal>(top)
        .SourcefixDispatchEdges<string, decimal, decimal>((subject, arrivals, targets) =>
        {
          foreach (var target in targets)
            target.Dispatch(target.Edge * 2);
        });

      CollectionAssert.AreEqual(
        new[] { ("top", "bottom", 0.50m, 0), ("top", "bottom", 1.50m, 1) },
        doubled.GetEdges().Select(e => (e.Parent, e.Child, e.Edge.Accumulate, e.InEdgeIndex)).ToArray());
    }

    [TestMethod]
    public void SourcefixDispatchEdges_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        Diamond().SourcefixDispatchEdges<string, decimal, decimal>((subject, arrivals, targets) => { }));
    }

    [TestMethod]
    public void SourcefixDispatch_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        Diamond().SourcefixDispatch(1m, (subject, arrivals, targets) =>
        {
          foreach (var target in targets.Skip(1))
            target.Dispatch(0m);
        }));
    }

    [TestMethod]
    public void SourcefixDispatch_ADoubleDispatchThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        Diamond().SourcefixDispatch(1m, (subject, arrivals, targets) =>
        {
          foreach (var target in targets)
            target.Dispatch(0m);
          targets[0].Dispatch(0m);
        }));
    }
  }
}
