using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The visit-stream conformance battery for the DAG traversal contract
  // (docs/DAG_CONTRACT_DESIGN.md): exact-stream pins on the canonical shapes, protocol
  // invariants over a corpus, the transpose duality (Transpose()'s walk must present every
  // forward edge reversed -- orientation is an OPERATOR, not a dimension), the topological
  // oracle (entry order must match
  // GetTopologicalOrder -- the builder is the family's oracle), and the strategy semantics,
  // rehearsal-tested at birth per the tree family's lesson.
  [TestClass]
  public class DagnumeratorConformanceTests
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

    private static IEnumerable<Dag<string, decimal>> Corpus()
    {
      // Single node.
      yield return new Dag<string, decimal>(new DagNode<string, decimal>("only"));

      // Chain.
      var chainRoot = new DagNode<string, decimal>("a");
      chainRoot.AddChild("b", 1m).AddChild("c", 1m);
      yield return new Dag<string, decimal>(chainRoot);

      // The diamond.
      yield return Diamond();

      // Shared leaf under three parents, two of them roots.
      var alpha = new DagNode<string, decimal>("alpha");
      var beta = new DagNode<string, decimal>("beta");
      var middle = alpha.AddChild("middle", 0.5m);
      var sharedLeaf = new DagNode<string, decimal>("sharedLeaf");
      alpha.AddChild(sharedLeaf, 0.1m);
      beta.AddChild(sharedLeaf, 0.2m);
      middle.AddChild(sharedLeaf, 0.3m);
      yield return new Dag<string, decimal>(alpha, beta);

      // Parallel edges (permitted; two discoveries, distinct edge indices).
      var top = new DagNode<string, decimal>("top");
      var bottom = new DagNode<string, decimal>("bottom");
      top.AddChild(bottom, 0.25m);
      top.AddChild(bottom, 0.75m);
      yield return new Dag<string, decimal>(top);

      // Two disconnected components.
      var island1 = new DagNode<string, decimal>("island1");
      island1.AddChild("island1Child", 1m);
      var island2 = new DagNode<string, decimal>("island2");
      yield return new Dag<string, decimal>(island1, island2);
    }

    // ---------------------------------------------------------------------------------------
    // Exact-stream pins.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PreEnumerationConvention_IsTheSentinel()
    {
      using var dagnumerator = Diamond().GetDagnumerator();

      Assert.AreEqual(DagnumeratorMode.DiscoveringNode, dagnumerator.Mode);
      Assert.AreEqual(-1, dagnumerator.Ordinal);
      Assert.AreEqual(-1, dagnumerator.ParentOrdinal);
      Assert.AreEqual(0, dagnumerator.EdgeIndex);
      Assert.IsNull(dagnumerator.Node);
      Assert.AreEqual(0m, dagnumerator.Edge);
    }

    [TestMethod]
    public void Diamond_ForwardStream_IsPinned()
    {
      // Topological order (discovery-biased): apex, left, right, venture. The venture's entry
      // fires only after BOTH discoveries -- the protocol's defining guarantee.
      using var dagnumerator = Diamond().GetDagnumerator();

      CollectionAssert.AreEqual(
        new[]
        {
          Discover("apex", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("apex", 0),
          Discover("left", 1, parentOrdinal: 0, edgeIndex: 0, edge: 0.60m),
          Discover("right", 2, parentOrdinal: 0, edgeIndex: 1, edge: 0.40m),
          Enter("left", 1),
          Discover("venture", 3, parentOrdinal: 1, edgeIndex: 0, edge: 0.70m),
          Enter("right", 2),
          Discover("venture", 3, parentOrdinal: 2, edgeIndex: 0, edge: 0.30m),
          Enter("venture", 3),
        },
        Drain(dagnumerator));
    }

    [TestMethod]
    public void Diamond_BackwardStream_IsPinned()
    {
      // The transpose walk -- the operator the retired backward dimension became (the 2026-08-02
      // re-founding): sources are the sinks; ordinals index the transpose's OWN topological
      // order, which is the reverse of the forward one (venture 0, right 1, left 2, apex 3), so
      // the old backward stream carries over verbatim; the apex enters last with two discoveries.
      using var dagnumerator = Diamond().Transpose().GetDagnumerator();

      CollectionAssert.AreEqual(
        new[]
        {
          Discover("venture", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("venture", 0),
          Discover("left", 2, parentOrdinal: 0, edgeIndex: 0, edge: 0.70m),
          Discover("right", 1, parentOrdinal: 0, edgeIndex: 1, edge: 0.30m),
          Enter("right", 1),
          Discover("apex", 3, parentOrdinal: 1, edgeIndex: 0, edge: 0.40m),
          Enter("left", 2),
          Discover("apex", 3, parentOrdinal: 2, edgeIndex: 0, edge: 0.60m),
          Enter("apex", 3),
        },
        Drain(dagnumerator));
    }

    [TestMethod]
    public void EmptyDag_StreamsNothing()
    {
      using var dagnumerator = new Dag<string, decimal>().GetDagnumerator();

      Assert.IsFalse(dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll));
    }

    [TestMethod]
    public void ParallelEdges_YieldOneDiscoveryPerEdge()
    {
      var top = new DagNode<string, decimal>("top");
      var bottom = new DagNode<string, decimal>("bottom");
      top.AddChild(bottom, 0.25m);
      top.AddChild(bottom, 0.75m);

      using var dagnumerator = new Dag<string, decimal>(top).GetDagnumerator();

      CollectionAssert.AreEqual(
        new[]
        {
          Discover("top", 0, parentOrdinal: -1, edgeIndex: 0),
          Enter("top", 0),
          Discover("bottom", 1, parentOrdinal: 0, edgeIndex: 0, edge: 0.25m),
          Discover("bottom", 1, parentOrdinal: 0, edgeIndex: 1, edge: 0.75m),
          Enter("bottom", 1),
        },
        Drain(dagnumerator));
    }

    // ---------------------------------------------------------------------------------------
    // Protocol invariants over the corpus.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void EveryNodeEntersExactlyOnce_AfterExactlyInDegreeDiscoveries()
    {
      foreach (var dag in Corpus())
      {
        var visits = Drain(dag.GetDagnumerator());
        var topologicalOrder = dag.GetTopologicalOrder();

        for (var ordinal = 0; ordinal < topologicalOrder.Count; ordinal++)
        {
          var entries = visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Ordinal == ordinal);
          var discoveries = visits.Count(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.Ordinal == ordinal);
          var inDegree = topologicalOrder[ordinal].ParentEdges.Count;

          Assert.AreEqual(1, entries, $"entries for {topologicalOrder[ordinal].Value}");
          Assert.AreEqual(Math.Max(inDegree, 1), discoveries,
            $"discoveries for {topologicalOrder[ordinal].Value} (sources get the conventional one)");
        }
      }
    }

    [TestMethod]
    public void AllDiscoveriesPrecedeTheEntry_AndParentsAreAlreadyEntered()
    {
      foreach (var dag in Corpus())
      {
        var visits = Drain(dag.GetDagnumerator());
        var entered = new HashSet<int>();

        foreach (var visit in visits)
        {
          if (visit.Mode == DagnumeratorMode.EnteringNode)
          {
            Assert.IsTrue(entered.Add(visit.Ordinal), "double entry");
            continue;
          }

          Assert.IsFalse(entered.Contains(visit.Ordinal), "discovery after entry");
          if (visit.ParentOrdinal >= 0)
            Assert.IsTrue(entered.Contains(visit.ParentOrdinal), "dispatch from an unentered parent");
        }
      }
    }

    [TestMethod]
    public void EntryOrder_MatchesTheTopologicalOracle()
    {
      foreach (var dag in Corpus())
      {
        var entries = Drain(dag.GetDagnumerator())
          .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
          .Select(v => v.Node)
          .ToList();

        CollectionAssert.AreEqual(
          dag.GetTopologicalOrder().Select(n => n.Value).ToList(),
          entries);
      }
    }

    [TestMethod]
    public void BackwardStream_SatisfiesTheProtocolInvariants()
    {
      // The same protocol, roles reversed: entries once each, discoveries per walked in-edge
      // (the dag's out-edges), every discovery before its entry, every dispatcher entered.
      foreach (var dag in Corpus())
      {
        var visits = Drain(dag.Transpose().GetDagnumerator());
        var reversedOrder = dag.GetTopologicalOrder().Reverse().ToList();
        var entered = new HashSet<int>();

        foreach (var visit in visits)
        {
          if (visit.Mode == DagnumeratorMode.EnteringNode)
          {
            Assert.IsTrue(entered.Add(visit.Ordinal), "double entry");
            continue;
          }

          Assert.IsFalse(entered.Contains(visit.Ordinal), "discovery after entry");
          if (visit.ParentOrdinal >= 0)
            Assert.IsTrue(entered.Contains(visit.ParentOrdinal), "dispatch from an unentered node");
        }

        for (var ordinal = 0; ordinal < reversedOrder.Count; ordinal++)
        {
          var walkedInDegree = reversedOrder[ordinal].ChildEdges.Count;
          Assert.AreEqual(
            Math.Max(walkedInDegree, 1),
            visits.Count(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.Ordinal == ordinal),
            $"backward discoveries for {reversedOrder[ordinal].Value}");
        }
      }
    }

    [TestMethod]
    public void BackwardEntryOrder_IsTheReversedTopologicalOracle()
    {
      foreach (var dag in Corpus())
      {
        var entries = Drain(dag.Transpose().GetDagnumerator())
          .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
          .Select(v => v.Node)
          .ToList();

        CollectionAssert.AreEqual(
          dag.GetTopologicalOrder().Reverse().Select(n => n.Value).ToList(),
          entries);
      }
    }

    [TestMethod]
    public void BackwardDiscoveries_AreExactlyTheForwardEdges_Reversed()
    {
      // The transpose duality, stated on content: every forward edge (parent -> child, payload)
      // appears backward as exactly one discovery of the parent dispatched by the child, same
      // payload -- and nothing else does. (Corpus values are unique per dag, so value pairs
      // identify edges; parallel edges are compared as a multiset via sorted sequences.)
      foreach (var dag in Corpus())
      {
        var forwardEdges = Drain(dag.GetDagnumerator())
          .Where(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.ParentOrdinal >= 0)
          .ToList();
        var forwardOrdinalValues = Drain(dag.GetDagnumerator())
          .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
          .ToDictionary(v => v.Ordinal, v => v.Node);

        var backwardVisits = Drain(dag.Transpose().GetDagnumerator());
        var backwardOrdinalValues = backwardVisits
          .Where(v => v.Mode == DagnumeratorMode.EnteringNode)
          .ToDictionary(v => v.Ordinal, v => v.Node);

        var forward = forwardEdges
          .Select(v => (Parent: forwardOrdinalValues[v.ParentOrdinal], Child: v.Node, v.Edge))
          .OrderBy(edge => edge).ToList();
        var backward = backwardVisits
          .Where(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.ParentOrdinal >= 0)
          .Select(v => (Parent: v.Node, Child: backwardOrdinalValues[v.ParentOrdinal], v.Edge))
          .OrderBy(edge => edge).ToList();

        CollectionAssert.AreEqual(forward, backward);
      }
    }

    // ---------------------------------------------------------------------------------------
    // Strategy semantics (rehearsal-tested at birth).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void SkipEdge_OnOneDiamondInEdge_TheSharedNodeStillEnters()
    {
      var visits = Drain(
        Diamond().GetDagnumerator(),
        visit => visit.Mode == DagnumeratorMode.DiscoveringNode && visit.Node == "venture" && visit.ParentOrdinal == 1
          ? DagTraversalStrategies.SkipEdge
          : DagTraversalStrategies.TraverseAll);

      Assert.AreEqual(1, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "venture"),
        "one live in-edge must suffice");
    }

    [TestMethod]
    public void SkipEdge_OnEveryInEdge_TheNodeNeverEnters()
    {
      var visits = Drain(
        Diamond().GetDagnumerator(),
        visit => visit.Mode == DagnumeratorMode.DiscoveringNode && visit.Node == "venture"
          ? DagTraversalStrategies.SkipEdge
          : DagTraversalStrategies.TraverseAll);

      Assert.AreEqual(2, visits.Count(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.Node == "venture"),
        "both discoveries are still presented -- severing is the consumer's verdict on each");
      Assert.AreEqual(0, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "venture"));
    }

    [TestMethod]
    public void SkipEdge_OnASourceDiscovery_KillsTheComponentReachableOnlyThroughIt()
    {
      var visits = Drain(
        Diamond().GetDagnumerator(),
        visit => visit.Node == "apex" && visit.Mode == DagnumeratorMode.DiscoveringNode
          ? DagTraversalStrategies.SkipEdge
          : DagTraversalStrategies.TraverseAll);

      // The conventional discovery was the apex's only in-edge; nothing below survives.
      CollectionAssert.AreEqual(
        new[] { Discover("apex", 0, parentOrdinal: -1, edgeIndex: 0) },
        visits);
    }

    [TestMethod]
    public void SkipOutEdges_KeepsTheNode_AndOnlyOtherPathsReachBelow()
    {
      // Suppress left's dispatch: the venture must still enter, via right's edge alone.
      var visits = Drain(
        Diamond().GetDagnumerator(),
        visit => visit.Mode == DagnumeratorMode.EnteringNode && visit.Node == "left"
          ? DagTraversalStrategies.SkipOutEdges
          : DagTraversalStrategies.TraverseAll);

      Assert.AreEqual(1, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "left"));
      var ventureDiscoveries = visits.Where(v => v.Mode == DagnumeratorMode.DiscoveringNode && v.Node == "venture").ToList();
      Assert.AreEqual(1, ventureDiscoveries.Count, "left's dispatch is suppressed; only right's edge appears");
      Assert.AreEqual(0.30m, ventureDiscoveries[0].Edge);
      Assert.AreEqual(1, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "venture"));
    }

    [TestMethod]
    public void SkipOutEdges_OnTheOnlyPath_TheNodeBelowNeverAppears()
    {
      var chainRoot = new DagNode<string, decimal>("a");
      chainRoot.AddChild("b", 1m).AddChild("c", 1m);

      var visits = Drain(
        new Dag<string, decimal>(chainRoot).GetDagnumerator(),
        visit => visit.Mode == DagnumeratorMode.EnteringNode && visit.Node == "b"
          ? DagTraversalStrategies.SkipOutEdges
          : DagTraversalStrategies.TraverseAll);

      Assert.AreEqual(0, visits.Count(v => v.Node == "c"), "c is reachable only through b's dispatch");
      Assert.AreEqual(1, visits.Count(v => v.Mode == DagnumeratorMode.EnteringNode && v.Node == "b"), "b itself is kept");
    }

    [TestMethod]
    public void WrongModeStrategies_Throw()
    {
      using (var dagnumerator = Diamond().GetDagnumerator())
      {
        Assert.IsTrue(dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll));
        Assert.AreEqual(DagnumeratorMode.DiscoveringNode, dagnumerator.Mode);
        Assert.ThrowsException<ArgumentException>(
          () => dagnumerator.MoveNext(DagTraversalStrategies.SkipOutEdges));
      }

      using (var dagnumerator = Diamond().GetDagnumerator())
      {
        Assert.IsTrue(dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll)); // sentinel -> D(apex)
        Assert.IsTrue(dagnumerator.MoveNext(DagTraversalStrategies.TraverseAll)); // D(apex) -> E(apex)
        Assert.AreEqual(DagnumeratorMode.EnteringNode, dagnumerator.Mode);
        Assert.ThrowsException<ArgumentException>(
          () => dagnumerator.MoveNext(DagTraversalStrategies.SkipEdge));
      }

      using (var dagnumerator = Diamond().GetDagnumerator())
      {
        Assert.ThrowsException<ArgumentException>(
          () => dagnumerator.MoveNext(DagTraversalStrategies.SkipEdge));
      }
    }

    [TestMethod]
    public void CyclicGraph_ThrowsAtAcquisition()
    {
      var first = new DagNode<string, decimal>("first");
      var second = first.AddChild("second", 1m);
      second.AddChild(first, 1m);

      Assert.ThrowsException<DagCycleException>(
        () => new Dag<string, decimal>(first).GetDagnumerator());
    }
  }
}
