using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The upward operators (design-docs/DAG_CONTRACT_DESIGN.md, phase 3): SinkfixScan (per-use
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

    // Source-ness is a STREAM fact, not a decoration: a discovery with no dispatching parent is
    // the conventional source discovery (result buffers carry no Sources list).
    private static List<TNode> Sources<TNode, TEdge>(IDagnumerable<TNode, TEdge> source)
    {
      var sources = new List<TNode>();

      using var walk = source.GetDagnumerator();
      while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
        if (walk.Mode == DagnumeratorMode.DiscoveringNode && walk.ParentOrdinal < 0)
          sources.Add(walk.Node);

      return sources;
    }

    // The post-pass upflow read at one node. An upward pass's arrivals sit in OUT-edge order, and
    // GetEdges yields a parent's out-edges contiguously in that same order -- so position within
    // the parent's block IS the arrival index. Provenance is the join, not a field on the result
    // (the split-homes ruling, 2026-08-05).
    private static (string From, decimal Amount, decimal Edge)[] UpflowsAt(
      DagBuffer<DagDispatchResult<(string Name, decimal Holding), decimal>, decimal> attributed,
      string entity)
      => attributed.GetEdges()
        .Where(edge => edge.Parent.Node.Name == entity)
        .Select((edge, outEdgeIndex) => (edge.Child.Node.Name, edge.Parent.Arrivals[outEdgeIndex], edge.Edge))
        .ToArray();

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
        counts.Values.Select(pairing => pairing.Accumulate).ToArray());
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

      CollectionAssert.AreEqual(
        new[] { "APEX" },
        Sources(scanned).Select(pairing => pairing.Accumulate).ToArray());

      // The result is a PAIRING over the source's shared structure: edges read off the buffer,
      // .Accumulate projects the fold's values.
      var edges = scanned.GetEdges()
        .Select(e => (Parent: e.Parent.Accumulate, Child: e.Child.Accumulate, Edge: e.Edge))
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
        .Values.Select(pairing => pairing.Accumulate).ToList();

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
        counts.Values.Select(pairing => pairing.Accumulate).ToArray(),
        "apex + right + venture, one path each");
    }

    // ---------------------------------------------------------------------------------------
    // SinkfixDispatch -- the upward-diamond attribution.
    // ---------------------------------------------------------------------------------------

    // The attribution survey: a node's total (own holding + what its children sent it) travels
    // up each in-edge scaled by that edge's ownership fraction.
    private static void AttributeUp(
      (string Name, decimal Holding) subject,
      IReadOnlyList<DagDispatchInflow<(string Name, decimal Holding), decimal, decimal>> arrivals,
      IReadOnlyList<DagDispatchTarget<(string Name, decimal Holding), decimal, decimal>> targets)
    {
      // No virtual family upward: value ORIGINATES in the nodes, so the pass runs unseeded and
      // sinks simply see no arrivals.
      var total = subject.Holding + arrivals.Sum(upflow => upflow.Value);
      foreach (var target in targets)
        target.Dispatch(total * target.Edge);
    }

    [TestMethod]
    public void SinkfixDispatch_AttributesTheVentureToItsUltimateOwner()
    {
      var attributed = ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);

      var byEntity = attributed.Values.ToDictionary(result => result.Node.Name, result => result);

      // The venture holds 1000 and originates everything (no upflows -- sinks receive none).
      Assert.AreEqual(0, byEntity["venture"].Arrivals.Count);

      // Each middle entity sees its edge's share of the venture.
      CollectionAssert.AreEqual(new[] { ("venture", 700m, 0.70m) }, UpflowsAt(attributed, "left"));
      CollectionAssert.AreEqual(new[] { ("venture", 300m, 0.30m) }, UpflowsAt(attributed, "right"));

      // The apex's lookthrough, attributed per route -- no double count, each upflow joined back
      // to its dispatcher (the child, in an upward pass). Arrivals sit in OUT-EDGE order, which
      // the derivation pins deliberately (a literal transpose walk would present them in
      // reverse-topological child order instead -- the per-group order trap, dodged).
      CollectionAssert.AreEqual(
        new[] { ("left", 420m, 0.60m), ("right", 120m, 0.40m) },
        UpflowsAt(attributed, "apex"));
      Assert.AreEqual(540m, byEntity["apex"].Arrivals.ToArray().Sum());
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
        .Values.Last().Accumulate;

      var attributedUp = ValuedDiamond()
        .SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp)
        .Values.Single(result => result.Node.Name == "apex")
        .Arrivals.ToArray().Sum();

      Assert.AreEqual(ownershipDown * 1000m, attributedUp, "54% of 1000, both ways");
    }

    [TestMethod]
    public void SinkfixDispatch_SurveysSourcesNever_AndEveryOtherNodeOnce()
    {
      var surveyed = new List<string>();

      ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(
        (subject, arrivals, targets) =>
        {
          surveyed.Add(subject.Name);
          foreach (var target in targets)
            target.Dispatch(0m);
        });

      CollectionAssert.AreEqual(new[] { "venture", "right", "left" }, surveyed,
        "the apex has no in-edges; its resolved inflows ARE the result");
    }

    [TestMethod]
    public void SinkfixDispatch_SourceNess_IsTheConventionalDiscovery()
    {
      // The retired IsSource decoration's replacement: source-ness is a stream fact, read off
      // the result buffer's own walk -- and upward it is exactly the never-surveyed node set
      // whose resolved arrivals ARE the attribution.
      var attributed = ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);

      CollectionAssert.AreEqual(
        new[] { "apex" },
        Sources(attributed).Select(result => result.Node.Name).ToArray());
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
      string subject,
      IReadOnlyList<DagDispatchInflow<string, decimal, decimal>> arrivals,
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
      // shape untouched; groups still sum to one. The result payloads are PAIRINGS (the
      // edge-pairing amendment): the conditioned value beside the stake it was computed from.
      CollectionAssert.AreEquivalent(
        new[] { ("GP", "X", 0m), ("Fund", "X", 1m), ("X", "Op", 1m) },
        conditioned.GetEdges().Select(e => (e.Parent, e.Child, e.Edge.Accumulate)).ToList());

      // The original distribution rides along, undamaged -- nothing to reconstruct.
      CollectionAssert.AreEquivalent(
        new[] { ("GP", "X", 0.002m), ("Fund", "X", 0.998m), ("X", "Op", 1m) },
        conditioned.GetEdges().Select(e => (e.Parent, e.Child, e.Edge.Edge)).ToList());

      // The distribution invariant survives conditioning: lookthrough is fully accounted.
      // (Project the pairing away first -- the doc's own idiom for values traveling on.)
      var lookthrough = conditioned
        .SelectEdges(e => e.Edge.Accumulate)
        .SourcefixScan<string, decimal, decimal>(
          (entity, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(i => i.Value * i.Edge));

      foreach (var pairing in lookthrough.Values)
        Assert.AreEqual(1m, pairing.Accumulate);
    }

    [TestMethod]
    public void SinkfixDispatchEdges_MoneyFollowsTheConditionedEdges()
    {
      var moved = GpSliver()
        .SinkfixDispatchEdges<string, decimal, decimal>(ConditionOutGp)
        .SelectEdges(e => e.Edge.Accumulate)
        .SourcefixDispatch(1_000m, (subject, arrivals, targets) =>
        {
          var arrived = arrivals.Sum(arrival => arrival.Value);

          // The virtual source family, surveyed first: both sources receive the seed verbatim
          // (its targets carry no payload to split by) -- the pre-re-founding semantics.
          if (subject is null)
          {
            foreach (var target in targets)
              target.Dispatch(arrived);
            return;
          }

          foreach (var target in targets)
            target.Dispatch(arrived * target.Edge);
        });

      CollectionAssert.AreEqual(
        new[] { ("GP", 0m), ("Fund", 1_000m) },
        moved.GetEdges()
          .Where(edge => edge.Child.Node == "X")
          .Select(edge => (edge.Parent.Node, edge.Child.Arrivals[edge.InEdgeIndex]))
          .ToArray(),
        "the GP edge is live and visibly zero; the fund's carries everything");
      Assert.AreEqual(
        1_000m,
        moved.Values.Single(result => result.Node == "Op").Arrivals.ToArray().Sum());
    }

    [TestMethod]
    public void SinkfixDispatchEdges_TheCascadeIsVisible_ChildrenResolveFirst()
    {
      // Reverse topological: Op's survey writes X->Op before X's survey runs, and X sees that
      // write among its out-edge results -- dispatcher, new payload, old payload.
      var seenAtX = new List<(string Dispatcher, decimal NewPayload, decimal OldPayload)>();

      GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((subject, arrivals, owners) =>
      {
        if (subject == "X")
          seenAtX.AddRange(arrivals.Select(i => (i.Dispatcher, i.Value, i.Edge)));

        ConditionOutGp(subject, arrivals, owners);
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
        .SinkfixDispatchEdges<string, decimal, decimal>((subject, arrivals, owners) =>
        {
          foreach (var owner in owners)
            owner.Dispatch(owner.Edge * 2);
        });

      CollectionAssert.AreEqual(
        new[] { ("top", "bottom", 0.50m, 0), ("top", "bottom", 1.50m, 1) },
        doubled.GetEdges().Select(e => (e.Parent, e.Child, e.Edge.Accumulate, e.InEdgeIndex)).ToArray(),
        "each parallel edge rewritten from its own slot, per-parent order preserved");
    }

    [TestMethod]
    public void SinkfixDispatchEdges_SurveysNonSourcesOnce_InReverseTopologicalOrder()
    {
      var surveyed = new List<string>();

      GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((subject, arrivals, owners) =>
      {
        surveyed.Add(subject);
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
        GpSliver().SinkfixDispatchEdges<string, decimal, decimal>((subject, arrivals, owners) => { }));
    }

    [TestMethod]
    public void SinkfixDispatch_AnUndispatchedTargetThrows()
    {
      Assert.ThrowsException<InvalidOperationException>(() =>
        ValuedDiamond().SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(
          (subject, arrivals, targets) => { }));
    }

    [TestMethod]
    public void SinkfixDispatch_ComposesWithAnUpstreamPrune()
    {
      // Blocker on the left: only the right route attributes upward.
      var attributed = ValuedDiamond()
        .PruneBefore(entity => entity.Name == "left")
        .SinkfixDispatch<(string Name, decimal Holding), decimal, decimal>(AttributeUp);

      CollectionAssert.AreEqual(
        new[] { ("right", 120m, 0.40m) },
        UpflowsAt(attributed, "apex"),
        "1000 x 30% x 40%, the surviving route only");
    }
  }
}
