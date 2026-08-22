using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The forward streaming operators (design-docs/DAG_CONTRACT_DESIGN.md, phase 2): exact-stream pins
  // on the ownership diamond (ordinal GAPS pinned deliberately -- operators preserve source
  // ordinals), chains, consumer-strategy passthrough, and content differentials against the
  // builder's own operator clones -- the spike earning its oracle role. Content, not stream:
  // the builder ops re-derive their own discovery-biased order, so entered-value sets and
  // surviving-edge multisets are the honest comparison.
  [TestClass]
  public class DagForwardOperatorTests
  {
    private readonly record struct Visit(
      DagnumeratorMode Mode, string Node, int Ordinal, int ParentOrdinal, int EdgeIndex, decimal Edge);

    private static Visit Discover(string node, int ordinal, int parentOrdinal, int edgeIndex, decimal edge = 0m)
      => new(DagnumeratorMode.DiscoveringNode, node, ordinal, parentOrdinal, edgeIndex, edge);

    private static Visit Enter(string node, int ordinal)
      => new(DagnumeratorMode.EnteringNode, node, ordinal, -1, 0, 0m);

    private static List<Visit> Drain(
      IDagnumerator<string, decimal> dagnumerator,
      Func<Visit, DagTraversalStrategies> strategySelector = null)
    {
      var visits = new List<Visit>();
      var strategies = DagTraversalStrategies.TraverseAll;

      while (dagnumerator.MoveNext(strategies))
      {
        var visit = new Visit(
          dagnumerator.Mode, dagnumerator.Node, dagnumerator.Ordinal,
          dagnumerator.ParentOrdinal, dagnumerator.EdgeIndex, dagnumerator.Edge);
        visits.Add(visit);
        strategies = strategySelector?.Invoke(visit) ?? DagTraversalStrategies.TraverseAll;
      }

      return visits;
    }

    // The ownership diamond: apex owns left 60% / right 40%; each owns the venture (70%/30%).
    // Source ordinals: apex 0, left 1, right 2, venture 3.
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

    // ---------------------------------------------------------------------------------------
    // Select.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Select_MapsValues_AndForwardsEverythingElse()
    {
      var mapped = Drain(Diamond().Select(n => n.ToUpperInvariant()).GetDagnumerator());
      var source = Drain(Diamond().GetDagnumerator());

      CollectionAssert.AreEqual(
        source.Select(v => v with { Node = v.Node.ToUpperInvariant() }).ToList(),
        mapped);
    }

    [TestMethod]
    public void Select_Chains()
    {
      var chained = Diamond()
        .Select(n => n.Length)
        .Select(length => $"#{length}");

      var entries = Drain(chained.GetDagnumerator())
        .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
        .Select(v => v.Node)
        .ToList();

      CollectionAssert.AreEqual(new List<string> { "#4", "#4", "#5", "#7" }, entries);
    }

    private static List<Visit> Drain(IDagnumerator<int, decimal> dagnumerator)
    {
      var visits = new List<Visit>();
      while (dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll))
        visits.Add(new Visit(
          dagnumerator.Mode, dagnumerator.Node.ToString(), dagnumerator.Ordinal,
          dagnumerator.ParentOrdinal, dagnumerator.EdgeIndex, dagnumerator.Edge));
      return visits;
    }

    // ---------------------------------------------------------------------------------------
    // Do.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Do_SeesEveryVisitInOrder_AndForwardsTheStreamUnchanged()
    {
      var seen = new List<Visit>();

      var forwarded = Drain(
        Diamond()
          .Do(visit => seen.Add(new Visit(visit.Mode, visit.Node, visit.Ordinal, visit.ParentOrdinal, visit.EdgeIndex, visit.Edge)))
          .GetDagnumerator());

      var source = Drain(Diamond().GetDagnumerator());

      CollectionAssert.AreEqual(source, forwarded, "Do must be a pure passthrough");
      CollectionAssert.AreEqual(source, seen, "the action sees exactly the published stream");
    }

    [TestMethod]
    public void Do_IsDeferred_TheActionFiresPerEnumeration()
    {
      // Cast to the contract deliberately: the oracle ALSO has a Do over the builder (the
      // spike's eager per-node action), and a parameter-discarding lambda binds to either. The
      // two Dos are semantically different ops sharing a name -- flagged for the naming cleanup.
      var invocations = 0;
      var wrapped = ((IDagnumerable<string, decimal>)Diamond()).Do(_ => invocations++);

      Assert.AreEqual(0, invocations, "constructing the wrapper runs nothing");

      Drain(wrapped.GetDagnumerator());
      var afterFirst = invocations;
      Drain(wrapped.GetDagnumerator());

      Assert.AreEqual(afterFirst * 2, invocations, "each enumeration replays the effects");
    }

    // ---------------------------------------------------------------------------------------
    // PruneBefore.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PruneBefore_DiamondLeft_IsPinned_WithTheOrdinalGap()
    {
      // left leaves the logical dag: its discovery is swallowed, its dispatch never happens,
      // and the venture arrives through right alone. Ordinal 1 is a GAP -- operators preserve
      // source ordinals; nothing relabels.
      CollectionAssert.AreEqual(
        new[]
        {
          Discover("apex", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("apex", 0),
          Discover("right", 2, parentOrdinal: 0, edgeIndex: 1, edge: 0.40m),
          Enter("right", 2),
          Discover("venture", 3, parentOrdinal: 2, edgeIndex: 0, edge: 0.30m),
          Enter("venture", 3),
        },
        Drain(Diamond().PruneBefore(n => n == "left").GetDagnumerator()));
    }

    [TestMethod]
    public void PruneBefore_TheApex_EmptiesTheStream()
    {
      CollectionAssert.AreEqual(
        Array.Empty<Visit>(),
        Drain(Diamond().PruneBefore(n => n == "apex").GetDagnumerator()));
    }

    [TestMethod]
    public void PruneBefore_BothMiddles_KillsTheVentureToo()
    {
      var visits = Drain(
        Diamond().PruneBefore(n => n == "left" || n == "right").GetDagnumerator());

      CollectionAssert.AreEqual(
        new[] { Discover("apex", 0, parentOrdinal: -1, edgeIndex: 0), Enter("apex", 0) },
        visits,
        "no live path reaches the venture");
    }

    [TestMethod]
    public void PruneBefore_MatchesTheBuilderOracle_OnContent()
    {
      // The blocker scenario shape: prune the middle tier's matching entities; compare the
      // logical dag against the builder's own PruneBefore clone -- entered values and the
      // surviving (parent, child, payload) edge multiset.
      var contract = Drain(Diamond().PruneBefore(n => n == "left").GetDagnumerator());
      var oracle = Diamond().OraclePruneBefore(node => node.Value == "left");

      CollectionAssert.AreEquivalent(
        oracle.GetTopologicalOrder().Select(n => n.Value).ToList(),
        contract.Where(v => v.Mode == DagnumeratorMode.EnteringNode).Select(v => v.Node).ToList());

      var contractValuesByOrdinal = contract
        .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
        .ToDictionary(v => v.Ordinal, v => v.Node);
      var contractEdges = contract
        .Where(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.ParentOrdinal >= 0)
        .Select(v => (Parent: contractValuesByOrdinal[v.ParentOrdinal], Child: v.Node, v.Edge))
        .OrderBy(edge => edge).ToList();
      var oracleEdges = oracle.GetTopologicalOrder()
        .SelectMany(n => n.ChildEdges.Select(e => (Parent: n.Value, Child: e.Child.Value, Edge: e.Value)))
        .OrderBy(edge => edge).ToList();

      CollectionAssert.AreEqual(oracleEdges, contractEdges);
    }

    // ---------------------------------------------------------------------------------------
    // PruneAfter.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PruneAfter_DiamondLeft_KeepsLeft_AndTheVentureArrivesThroughRight()
    {
      CollectionAssert.AreEqual(
        new[]
        {
          Discover("apex", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("apex", 0),
          Discover("left", 1, parentOrdinal: 0, edgeIndex: 0, edge: 0.60m),
          Discover("right", 2, parentOrdinal: 0, edgeIndex: 1, edge: 0.40m),
          Enter("left", 1),
          Enter("right", 2),
          Discover("venture", 3, parentOrdinal: 2, edgeIndex: 0, edge: 0.30m),
          Enter("venture", 3),
        },
        Drain(Diamond().PruneAfter(n => n == "left").GetDagnumerator()),
        "left enters but dispatches nothing");
    }

    [TestMethod]
    public void PruneAfter_OnTheOnlyPath_TheNodeBelowNeverAppears()
    {
      var chainRoot = new DagNode<string, decimal>("a");
      chainRoot.AddChild("b", 1m).AddChild("c", 1m);

      var visits = Drain(
        new Dag<string, decimal>(chainRoot).PruneAfter(n => n == "b").GetDagnumerator());

      CollectionAssert.AreEqual(
        new[]
        {
          Discover("a", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("a", 0),
          Discover("b", 1, parentOrdinal: 0, edgeIndex: 0, edge: 1m),
          Enter("b", 1),
        },
        visits);
    }

    [TestMethod]
    public void PruneAfter_MatchesTheBuilderOracle_OnContent()
    {
      var contract = Drain(Diamond().PruneAfter(n => n == "left").GetDagnumerator());
      var oracle = Diamond().OraclePruneAfter(node => node.Value == "left");

      CollectionAssert.AreEquivalent(
        oracle.GetTopologicalOrder().Select(n => n.Value).ToList(),
        contract.Where(v => v.Mode == DagnumeratorMode.EnteringNode).Select(v => v.Node).ToList());
    }

    // ---------------------------------------------------------------------------------------
    // Chains and consumer strategies through wrappers.
    // ---------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------
    // The edge dual: SelectEdges / PruneEdges.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SelectEdges_MapsPayloads_WithTheFullRelationshipInScope()
    {
      // Fractions to basis points, the relationship context available (parent named in the
      // result proves it): node values, structure, ordinals all forwarded unchanged.
      var basisPoints = Diamond().SelectEdges(e => $"{e.Parent}->{e.Child}:{(int)(e.Edge * 10_000)}");

      var edges = new List<string>();
      using (var walk = basisPoints.GetDagnumerator())
        while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
          if (walk.Mode == DagnumeratorMode.DiscoveringNode && walk.ParentOrdinal >= 0)
            edges.Add(walk.Edge);

      CollectionAssert.AreEqual(
        new[] { "apex->left:6000", "apex->right:4000", "left->venture:7000", "right->venture:3000" },
        edges);
    }

    [TestMethod]
    public void PruneEdges_TargetedRelationship_TheChildSurvivesViaItsOtherEdge()
    {
      // The GP shape: sever ONE relationship -- both endpoints untouched, the child lives on
      // via its other in-edge, and the severed edge never reaches consumers.
      var pruned = Diamond().PruneEdges(e => e.Parent == "left" && e.Child == "venture");

      CollectionAssert.AreEqual(
        new[] { ("apex", "left", 0.60m), ("apex", "right", 0.40m), ("right", "venture", 0.30m) },
        pruned.GetEdges().Select(e => (e.Parent, e.Child, e.Edge)).ToArray());

      var entries = Drain(pruned.GetDagnumerator())
        .Where(v => v.Mode == DagnumeratorMode.EnteringNode).Select(v => v.Node).ToList();
      CollectionAssert.AreEqual(new List<string> { "apex", "left", "right", "venture" }, entries,
        "left keeps existing; the venture keeps existing; only the relationship died");
    }

    [TestMethod]
    public void PruneEdges_AllInEdges_TheChildVanishesByLiveness()
    {
      var visits = Drain(
        Diamond().PruneEdges(e => e.Child == "venture").GetDagnumerator());

      Assert.AreEqual(0, visits.Count(v => v.Node == "venture"),
        "no live in-edge, no entry -- the liveness fold, not the operator, removes the node");
    }

    [TestMethod]
    public void PruneEdges_ComposesIntoAWeightNormalizingDispatch()
    {
      // The conditioning composition, general-purpose spelling: sever the relationship, then a
      // weight-NORMALIZING survey renormalizes over the survivors at the right level. (An
      // absolute-fraction survey would NOT -- rebalancing facts is the caller's group algebra;
      // the operator only removes.)
      var moved = Diamond()
        .PruneEdges(e => e.Parent == "left" && e.Child == "venture")
        .SourcefixDispatch(1000m, (subject, arrivals, targets) =>
        {
          var arrived = arrivals.Sum(arrival => arrival.Value);

          // The virtual source family fires first (full participation): its targets
          // are the sources and carry no payload, so there is no weight to normalize by -- the
          // seed reaches each source verbatim, the pre-re-founding semantics, now authored.
          if (subject is null)
          {
            foreach (var target in targets)
              target.Dispatch(arrived);
            return;
          }

          var totalWeight = targets.Sum(target => target.Edge);
          foreach (var target in targets)
            target.Dispatch(arrived * target.Edge / totalWeight);
        });

      var byEntity = moved.Values.ToDictionary(result => result.Node, result => result);

      Assert.AreEqual(400m, byEntity["venture"].Arrivals.ToArray().Sum(),
        "the venture's whole funding rides right's edge -- 100% of the surviving weight");
      Assert.AreEqual(600m, byEntity["left"].Arrivals.ToArray().Sum(),
        "left still receives -- only its edge to the venture died, not the entity");
    }

    [TestMethod]
    public void PruneThenSelect_Chains()
    {
      var entries = Drain(
        Diamond()
          .PruneBefore(n => n == "left")
          .Select(n => n.ToUpperInvariant())
          .GetDagnumerator())
        .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
        .Select(v => v.Node)
        .ToList();

      CollectionAssert.AreEqual(new List<string> { "APEX", "RIGHT", "VENTURE" }, entries);
    }

    [TestMethod]
    public void ConsumerStrategies_FlowThroughWrappers()
    {
      // The consumer suppresses LEFT's dispatch through a Select wrapper: the wrapper forwards
      // the verdict, and the venture arrives via right alone.
      var visits = Drain(
        Diamond().Select(n => n.ToUpperInvariant()).GetDagnumerator(),
        visit => visit.Mode == DagnumeratorMode.EnteringNode && visit.Node == "LEFT"
          ? DagTraversalStrategies.SkipOutEdges
          : DagTraversalStrategies.TraverseAll);

      var ventureDiscoveries = visits
        .Where(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.Node == "VENTURE")
        .ToList();

      Assert.AreEqual(1, ventureDiscoveries.Count);
      Assert.AreEqual(0.30m, ventureDiscoveries[0].Edge);
      Assert.AreEqual(1, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "VENTURE"));
    }

    [TestMethod]
    public void WrongModeStrategies_StillThrow_ThroughWrappers()
    {
      using var dagnumerator = Diamond().Select(n => n).GetDagnumerator();

      Assert.IsTrue(dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(DagnumeratorMode.DiscoveringNode, dagnumerator.Mode);
      Assert.ThrowsException<ArgumentException>(
        () => dagnumerator.MoveNext(DagTraversalStrategies.SkipOutEdges));
    }
  }
}
