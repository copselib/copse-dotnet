using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The subgraph-selection cluster (flow-direction naming + the upstream mirror): GetSources /
  // GetSinks (the boundary drains) and the closure selectors
  // TakeDownstreamWhere / TakeUpstreamWhere. The load-bearing pins: the result's boundary is
  // EMERGENT (downstream: a match reachable from another match comes out interior, so result
  // sources are the unswept matches; upstream: a match that reaches another match comes out
  // interior, so result sinks are the matches reaching no further match); shared structure
  // appears once; edges to excluded territory die (downstream: with their parents; upstream:
  // with their children); the two selectors are transpose conjugates -- the pinned law.
  [TestClass]
  public class DagSubgraphSelectionTests
  {
    // Two disconnected chains sharing nothing: a->b and c (a lone source-sink).
    private static Dag<string, decimal> ChainAndLoner()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m);
      var c = new DagNode<string, decimal>("c");
      return new Dag<string, decimal>(a, c);
    }

    private static string[] Edges<TNode>(IDagnumerable<TNode, decimal> dag) =>
      dag.GetEdges().Select(e => $"{e.Parent}->{e.Child}:{e.Edge}").ToArray();

    // ---------------------------------------------------------------------------------------
    // GetSources / GetSinks.

    [TestMethod]
    public void GetSources_Diamond_IsTheApex()
    {
      CollectionAssert.AreEqual(new[] { "apex" }, DagWalkerCorpus.Diamond().GetSources().ToArray());
    }

    [TestMethod]
    public void GetSinks_Diamond_IsTheVenture()
    {
      CollectionAssert.AreEqual(new[] { "venture" }, DagWalkerCorpus.Diamond().GetSinks().ToArray());
    }

    [TestMethod]
    public void GetSources_And_GetSinks_MultiComponent_InTopologicalOrder()
    {
      var dag = ChainAndLoner();

      CollectionAssert.AreEqual(new[] { "a", "c" }, dag.GetSources().ToArray());
      // A lone node is both: flow begins and ends at it.
      CollectionAssert.AreEqual(new[] { "b", "c" }, dag.GetSinks().ToArray());
    }

    [TestMethod]
    public void GetSources_ComposesOverAWrapper()
    {
      // Pruning the apex kills everything (closure death via the liveness fold): no sources.
      Assert.AreEqual(0, DagWalkerCorpus.Diamond().PruneNodesBefore(n => n == "apex").GetSources().Count());

      // Pruning the venture leaves the apex the sole source, untouched.
      CollectionAssert.AreEqual(
        new[] { "apex" }, DagWalkerCorpus.Diamond().PruneNodesBefore(n => n == "venture").GetSources().ToArray());
    }

    // ---------------------------------------------------------------------------------------
    // TakeDownstreamWhere.

    [TestMethod]
    public void TakeDownstreamWhere_OneMiddle_TakesItsClosure_AndTheOutsideEdgeDies()
    {
      var selected = DagWalkerCorpus.Diamond().TakeDownstreamWhere(n => n == "left");

      CollectionAssert.AreEqual(new[] { "left", "venture" }, selected.GetTopologicalOrder().ToArray());
      // right->venture died with its outside parent; left's whole block survived.
      CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, Edges(selected));
      CollectionAssert.AreEqual(new[] { "left" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "venture" }, selected.GetSinks().ToArray());
    }

    [TestMethod]
    public void TakeDownstreamWhere_BothMiddles_ShareTheVentureOnce()
    {
      var selected = DagWalkerCorpus.Diamond().TakeDownstreamWhere(n => n == "left" || n == "right");

      // One result dag, two sources, the shared descendant present ONCE with both in-edges --
      // a second path into included structure is an edge, not a copy.
      Assert.AreEqual(3, selected.Count);
      CollectionAssert.AreEqual(new[] { "left", "right", "venture" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left", "right" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "left->venture:0.70", "right->venture:0.30" }, Edges(selected));
    }

    [TestMethod]
    public void TakeDownstreamWhere_NestedMatch_DissolvesIntoTheInterior()
    {
      // venture matches too, but left's closure already holds it: it comes out an interior
      // node, NOT a second source -- the no-nested-matches rule, emergent from in-degree.
      var selected = DagWalkerCorpus.Diamond().TakeDownstreamWhere(n => n == "left" || n == "venture");

      CollectionAssert.AreEqual(new[] { "left", "venture" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, Edges(selected));
    }

    [TestMethod]
    public void TakeDownstreamWhere_MatchingNothing_IsEmpty()
    {
      var selected = DagWalkerCorpus.Diamond().TakeDownstreamWhere(_ => false);

      Assert.AreEqual(0, selected.Count);
      Assert.AreEqual(0, selected.GetTopologicalOrder().Count);
      Assert.AreEqual(0, selected.GetSources().Count());
    }

    [TestMethod]
    public void TakeDownstreamWhere_FullClosure_ReturnsTheBufferItself()
    {
      // Matching the apex sweeps in everything; on a buffer source the operator returns the
      // buffer (immutable, so identity is safe -- Materialize's own convention).
      var buffer = DagWalkerCorpus.Diamond().Materialize();

      Assert.AreSame(buffer, buffer.TakeDownstreamWhere(n => n == "apex"));
      Assert.AreEqual(4, buffer.TakeDownstreamWhere(n => n == "apex").Count);
    }

    [TestMethod]
    public void TakeDownstreamWhere_SourceOrdinals_CorrelateBackToTheCapturedStream()
    {
      var selected = DagWalkerCorpus.Diamond().TakeDownstreamWhere(n => n == "left");

      // Result ordinals 0,1 are the capture's 1 (left) and 3 (venture).
      Assert.AreEqual(1, selected.SourceOrdinal(0));
      Assert.AreEqual(3, selected.SourceOrdinal(1));
    }

    [TestMethod]
    public void TakeDownstreamWhere_ResultComposes_ThroughTheFluentSurface()
    {
      // The result is an IDagnumerable like any other: operators chain on it.
      var reselected = DagWalkerCorpus.Diamond()
        .TakeDownstreamWhere(n => n == "left" || n == "right")
        .SelectNodes(n => n.ToUpperInvariant())
        .TakeDownstreamWhere(n => n == "RIGHT");

      CollectionAssert.AreEqual(new[] { "RIGHT", "VENTURE" }, reselected.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void TakeDownstreamWhere_NullPredicate_Throws()
    {
      Assert.ThrowsException<ArgumentNullException>(() => DagWalkerCorpus.Diamond().TakeDownstreamWhere(null));
    }

    // ---------------------------------------------------------------------------------------
    // TakeUpstreamWhere.

    [TestMethod]
    public void TakeUpstreamWhere_OneMiddle_TakesItsAncestry_AndTheOutsideEdgeDies()
    {
      var selected = DagWalkerCorpus.Diamond().TakeUpstreamWhere(n => n == "left");

      CollectionAssert.AreEqual(new[] { "apex", "left" }, selected.GetTopologicalOrder().ToArray());
      // apex->right died with its outside child; left->venture died below the match.
      CollectionAssert.AreEqual(new[] { "apex->left:0.60" }, Edges(selected));
      CollectionAssert.AreEqual(new[] { "apex" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "left" }, selected.GetSinks().ToArray());
    }

    [TestMethod]
    public void TakeUpstreamWhere_BothMiddles_ShareTheApexOnce()
    {
      var selected = DagWalkerCorpus.Diamond().TakeUpstreamWhere(n => n == "left" || n == "right");

      // One result dag, two sinks, the shared ancestor present ONCE with both out-edges.
      Assert.AreEqual(3, selected.Count);
      CollectionAssert.AreEqual(new[] { "apex", "left", "right" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left", "right" }, selected.GetSinks().ToArray());
      CollectionAssert.AreEqual(new[] { "apex->left:0.60", "apex->right:0.40" }, Edges(selected));
    }

    [TestMethod]
    public void TakeUpstreamWhere_NestedMatch_DissolvesIntoTheInterior()
    {
      // a matches too, but a reaches b: it comes out an interior node, NOT a second sink --
      // the downstream emergence mirrored (result sinks = matches reaching no further match).
      var selected = ChainAndLoner().TakeUpstreamWhere(n => n == "a" || n == "b");

      CollectionAssert.AreEqual(new[] { "a", "b" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "b" }, selected.GetSinks().ToArray());
      CollectionAssert.AreEqual(new[] { "a->b:1" }, Edges(selected));
    }

    [TestMethod]
    public void TakeUpstreamWhere_MatchingNothing_IsEmpty()
    {
      var selected = DagWalkerCorpus.Diamond().TakeUpstreamWhere(_ => false);

      Assert.AreEqual(0, selected.Count);
      Assert.AreEqual(0, selected.GetTopologicalOrder().Count);
      Assert.AreEqual(0, selected.GetSinks().Count());
    }

    [TestMethod]
    public void TakeUpstreamWhere_FullClosure_ReturnsTheBufferItself()
    {
      // Matching the venture sweeps in everything upstream of it -- the whole diamond.
      var buffer = DagWalkerCorpus.Diamond().Materialize();

      Assert.AreSame(buffer, buffer.TakeUpstreamWhere(n => n == "venture"));
      Assert.AreEqual(4, buffer.TakeUpstreamWhere(n => n == "venture").Count);
    }

    [TestMethod]
    public void TakeUpstreamWhere_SourceOrdinals_CorrelateBackToTheCapturedStream()
    {
      var selected = DagWalkerCorpus.Diamond().TakeUpstreamWhere(n => n == "right");

      // Result ordinals 0,1 are the capture's 0 (apex) and 2 (right).
      Assert.AreEqual(0, selected.SourceOrdinal(0));
      Assert.AreEqual(2, selected.SourceOrdinal(1));
    }

    [TestMethod]
    public void TakeUpstreamWhere_EqualsTheTransposeConjugate()
    {
      // The pinned law: TakeUpstreamWhere(p) ≡ Transpose().TakeDownstreamWhere(p).Transpose().
      // Content-canonical comparison per the readiness clause (the conjugate may present
      // another topological order).
      foreach (var predicate in new Func<string, bool>[]
               {
                 n => n == "right",
                 n => n == "left" || n == "right",
                 n => n == "venture",
                 _ => false,
               })
      {
        var direct = DagWalkerCorpus.Diamond().TakeUpstreamWhere(predicate);
        var conjugate = DagWalkerCorpus.Diamond().Transpose().TakeDownstreamWhere(predicate).Transpose();

        CollectionAssert.AreEqual(
          direct.GetTopologicalOrder().OrderBy(n => n).ToArray(),
          conjugate.GetTopologicalOrder().OrderBy(n => n).ToArray());
        CollectionAssert.AreEqual(
          Edges(direct).OrderBy(e => e).ToArray(),
          Edges(conjugate).OrderBy(e => e).ToArray());
      }
    }

    [TestMethod]
    public void TakeDownstreamWhere_ThenUpstream_IsTheBetweenGraph()
    {
      // "Downstream of left, upstream of the venture": every path from left down to the
      // venture and nothing else -- the between-graph, free as a composition.
      var between = DagWalkerCorpus.Diamond()
        .TakeDownstreamWhere(n => n == "left")
        .TakeUpstreamWhere(n => n == "venture");

      CollectionAssert.AreEqual(new[] { "left", "venture" }, between.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, Edges(between));
    }

    [TestMethod]
    public void TakeUpstreamWhere_ResultComposes_ThroughTheFluentSurface()
    {
      var reselected = DagWalkerCorpus.Diamond()
        .TakeUpstreamWhere(n => n == "left" || n == "right")
        .SelectNodes(n => n.ToUpperInvariant())
        .TakeUpstreamWhere(n => n == "RIGHT");

      CollectionAssert.AreEqual(new[] { "APEX", "RIGHT" }, reselected.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void TakeUpstreamWhere_NullPredicate_Throws()
    {
      Assert.ThrowsException<ArgumentNullException>(() => DagWalkerCorpus.Diamond().TakeUpstreamWhere(null));
    }
  }
}
