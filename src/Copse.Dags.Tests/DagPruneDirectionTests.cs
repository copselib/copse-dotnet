using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // Prune is TEMPORAL, not spatial (design-docs/DAG_CONTRACT_DESIGN.md, the prune clause,
  // 2026-07-28): "before"/"after" mean before/after the node in TRAVERSAL order, so the
  // prunes are dimension-relative. The 2x2 on the chain a->b->c is the whole theorem in
  // four pins: forward-before {a}, forward-after {a->b}, backward-before {c},
  // backward-after {b->c} -- same predicate, opposite halves removed. The forward pair
  // rides the shipped operators; the backward pair pins the SEMANTICS at the strategy
  // level (the surface spelling arrives as Transpose().Prune...() when Transpose lands --
  // the clause's ruling, since a composite prune overload would present a different dag
  // per dimension). Plus the disconnection pin: prune may split the dag into components,
  // and that is a legal outcome, not a hazard.
  [TestClass]
  public class DagPruneDirectionTests
  {
    private sealed record Visit(DagnumeratorMode Mode, string Node);

    private static List<Visit> Drain(
      IDagnumerator<string, decimal> dagnumerator,
      Func<Visit, DagTraversalStrategies> strategySelector = null)
    {
      var visits = new List<Visit>();
      var strategies = DagTraversalStrategies.TraverseAll;

      while (dagnumerator.MoveNext(strategies))
      {
        var visit = new Visit(dagnumerator.Mode, dagnumerator.Node);
        visits.Add(visit);
        strategies = strategySelector?.Invoke(visit) ?? DagTraversalStrategies.TraverseAll;
      }

      return visits;
    }

    private static List<string> EnteredNodes(IEnumerable<Visit> visits) =>
      visits.Where(visit => visit.Mode == DagnumeratorMode.EnteringNode).Select(visit => visit.Node).ToList();

    private static Dag<string, decimal> Chain()
    {
      var a = new DagNode<string, decimal>("a");
      a.AddChild("b", 1m).AddChild("c", 1m);
      return new Dag<string, decimal>(a);
    }

    [TestMethod]
    public void Forward_PruneBefore_RemovesTheNodeAndItsExclusiveDescendantSide()
    {
      var pruned = Chain().PruneBefore(node => node == "b");

      using var dagnumerator = pruned.GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "a" }, EnteredNodes(visits));
    }

    [TestMethod]
    public void Forward_PruneAfter_KeepsTheNodeAsANewSink()
    {
      var pruned = Chain().PruneAfter(node => node == "b");

      using var dagnumerator = pruned.GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "a", "b" }, EnteredNodes(visits));
      Assert.IsFalse(visits.Any(visit => visit.Node == "c"), "c must not be discovered: b dispatches nothing.");
    }

    [TestMethod]
    public void Backward_PruneBefore_RemovesTheNodeAndItsExclusiveAncestorSide()
    {
      // Backward = the transpose walked forward (order c, b, a): severing every discovery of b kills b, and a
      // -- b's exclusive reach IN THIS DIMENSION -- dies with it.
      using var dagnumerator = Chain().Transpose().GetDagnumerator();

      var visits = Drain(
        dagnumerator,
        visit => visit.Mode == DagnumeratorMode.DiscoveringNode && visit.Node == "b"
          ? DagTraversalStrategies.SkipEdge
          : DagTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(new[] { "c" }, EnteredNodes(visits));
    }

    [TestMethod]
    public void Backward_PruneAfter_KeepsTheNodeAsANewSource()
    {
      // b enters, then dispatches nothing backward: a starves. In original orientation the
      // survivor is b->c, the mirror of the forward-after pin -- same predicate, opposite
      // half removed.
      using var dagnumerator = Chain().Transpose().GetDagnumerator();

      var visits = Drain(
        dagnumerator,
        visit => visit.Mode == DagnumeratorMode.EnteringNode && visit.Node == "b"
          ? DagTraversalStrategies.SkipOutEdges
          : DagTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(new[] { "c", "b" }, EnteredNodes(visits));
      Assert.IsFalse(visits.Any(visit => visit.Node == "a"), "a must not be discovered: b dispatches nothing backward.");
    }

    [TestMethod]
    public void PruneBefore_MaySplitTheDagIntoComponents_LegallyNotHazardously()
    {
      // alpha -> x, alpha -> mid, beta -> mid, beta -> y: pruning mid disconnects the dag
      // into two islands, alpha->x and beta->y. Multi-component dags are first-class from
      // birth (the two-islands corpus fixture), so every invariant just holds.
      var alpha = new DagNode<string, decimal>("alpha");
      var beta = new DagNode<string, decimal>("beta");
      alpha.AddChild("x", 1m);
      var mid = alpha.AddChild("mid", 1m);
      beta.AddChild(mid, 1m);
      beta.AddChild("y", 1m);
      var dag = new Dag<string, decimal>(alpha, beta);

      var pruned = dag.PruneBefore(node => node == "mid");

      using var dagnumerator = pruned.GetDagnumerator();
      var entered = EnteredNodes(Drain(dagnumerator));

      CollectionAssert.AreEquivalent(new[] { "alpha", "beta", "x", "y" }, entered);
    }
  }
}
