using System;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The subgraph-selection cluster (2026-08-06): GetSources / GetSinks (the boundary drains)
  // and TakeSubgraphsWhere (the closure selector). The load-bearing pins: the result's sources are
  // EMERGENT (a match reachable from another match comes out interior -- the tree operator's
  // no-nested-matches flag as arithmetic, not a rule); shared descendants appear once; edges
  // from outside the closure die with their parents; ancestry-directed selection is the
  // transpose composition.
  [TestClass]
  public class DagSubgraphSelectionTests
  {
    // The ownership diamond of the forward-operator battery: apex owns left 60% / right 40%;
    // each owns the venture (70%/30%). Source ordinals: apex 0, left 1, right 2, venture 3.
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
      CollectionAssert.AreEqual(new[] { "apex" }, Diamond().GetSources().ToArray());
    }

    [TestMethod]
    public void GetSinks_Diamond_IsTheVenture()
    {
      CollectionAssert.AreEqual(new[] { "venture" }, Diamond().GetSinks().ToArray());
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
      Assert.AreEqual(0, Diamond().PruneBefore(n => n == "apex").GetSources().Count());

      // Pruning the venture leaves the apex the sole source, untouched.
      CollectionAssert.AreEqual(
        new[] { "apex" }, Diamond().PruneBefore(n => n == "venture").GetSources().ToArray());
    }

    // ---------------------------------------------------------------------------------------
    // TakeSubgraphsWhere.

    [TestMethod]
    public void TakeSubgraphsWhere_OneMiddle_TakesItsClosure_AndTheOutsideEdgeDies()
    {
      var selected = Diamond().TakeSubgraphsWhere(n => n == "left");

      CollectionAssert.AreEqual(new[] { "left", "venture" }, selected.GetTopologicalOrder().ToArray());
      // right->venture died with its outside parent; left's whole block survived.
      CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, Edges(selected));
      CollectionAssert.AreEqual(new[] { "left" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "venture" }, selected.GetSinks().ToArray());
    }

    [TestMethod]
    public void TakeSubgraphsWhere_BothMiddles_ShareTheVentureOnce()
    {
      var selected = Diamond().TakeSubgraphsWhere(n => n == "left" || n == "right");

      // One result dag, two sources, the shared descendant present ONCE with both in-edges --
      // a second path into included structure is an edge, not a copy.
      Assert.AreEqual(3, selected.Count);
      CollectionAssert.AreEqual(new[] { "left", "right", "venture" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left", "right" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "left->venture:0.70", "right->venture:0.30" }, Edges(selected));
    }

    [TestMethod]
    public void TakeSubgraphsWhere_NestedMatch_DissolvesIntoTheInterior()
    {
      // venture matches too, but left's closure already holds it: it comes out an interior
      // node, NOT a second source -- the no-nested-matches rule, emergent from in-degree.
      var selected = Diamond().TakeSubgraphsWhere(n => n == "left" || n == "venture");

      CollectionAssert.AreEqual(new[] { "left", "venture" }, selected.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "left" }, selected.GetSources().ToArray());
      CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, Edges(selected));
    }

    [TestMethod]
    public void TakeSubgraphsWhere_MatchingNothing_IsEmpty()
    {
      var selected = Diamond().TakeSubgraphsWhere(_ => false);

      Assert.AreEqual(0, selected.Count);
      Assert.AreEqual(0, selected.GetTopologicalOrder().Count);
      Assert.AreEqual(0, selected.GetSources().Count());
    }

    [TestMethod]
    public void TakeSubgraphsWhere_FullClosure_ReturnsTheBufferItself()
    {
      // Matching the apex sweeps in everything; on a buffer source the operator returns the
      // buffer (immutable, so identity is safe -- Materialize's own convention).
      var buffer = Diamond().Materialize();

      Assert.AreSame(buffer, buffer.TakeSubgraphsWhere(n => n == "apex"));
      Assert.AreEqual(4, buffer.TakeSubgraphsWhere(n => n == "apex").Count);
    }

    [TestMethod]
    public void TakeSubgraphsWhere_SourceOrdinals_CorrelateBackToTheCapturedStream()
    {
      var selected = Diamond().TakeSubgraphsWhere(n => n == "left");

      // Result ordinals 0,1 are the capture's 1 (left) and 3 (venture).
      Assert.AreEqual(1, selected.SourceOrdinal(0));
      Assert.AreEqual(3, selected.SourceOrdinal(1));
    }

    [TestMethod]
    public void TakeSubgraphsWhere_ThroughTheTranspose_SelectsAncestry()
    {
      // "Everything that REACHES left": the transpose composition the operator doc names.
      var ancestry = Diamond().Transpose().TakeSubgraphsWhere(n => n == "left").Transpose();

      CollectionAssert.AreEqual(new[] { "apex", "left" }, ancestry.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "apex->left:0.60" }, Edges(ancestry));
    }

    [TestMethod]
    public void TakeSubgraphsWhere_ResultComposes_ThroughTheFluentSurface()
    {
      // The result is an IDagnumerable like any other: operators chain on it.
      var reselected = Diamond()
        .TakeSubgraphsWhere(n => n == "left" || n == "right")
        .Select(n => n.ToUpperInvariant())
        .TakeSubgraphsWhere(n => n == "RIGHT");

      CollectionAssert.AreEqual(new[] { "RIGHT", "VENTURE" }, reselected.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void TakeSubgraphsWhere_NullPredicate_Throws()
    {
      Assert.ThrowsException<ArgumentNullException>(() => Diamond().TakeSubgraphsWhere(null));
    }
  }
}
