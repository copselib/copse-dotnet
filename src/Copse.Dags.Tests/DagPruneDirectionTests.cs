using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Copse.Dags.Tests.Visits;

namespace Copse.Dags.Tests
{
  // Prune is TEMPORAL, not spatial (design-docs/DAG_CONTRACT_DESIGN.md, the prune clause):
  // "before"/"after" mean before/after the node in TRAVERSAL order, so the prunes are
  // orientation-relative. The 2x2 on the chain a->b->c is the whole theorem in
  // four pins: forward-before {a}, forward-after {a->b}, backward-before {c},
  // backward-after {b->c} -- same predicate, opposite halves removed. The forward pair
  // rides the operators on the chain; the backward pair rides them on the TRANSPOSE -- the
  // clause's spelling, since a composite prune overload would present a different dag per
  // orientation. Plus the disconnection pin: prune may split the dag into components,
  // and that is a legal outcome, not a hazard.
  [TestClass]
  public class DagPruneDirectionTests
  {
    private static List<string> EnteredNodes(IEnumerable<Visit> visits) =>
      visits.Where(visit => visit.Mode == DagnumeratorMode.EnteringNode).Select(visit => visit.Node).ToList();

    [TestMethod]
    public void Forward_PruneNodesBefore_RemovesTheNodeAndItsExclusiveDescendantSide()
    {
      var pruned = DagWalkerCorpus.Chain().PruneNodesBefore(node => node == "b");

      using var dagnumerator = pruned.GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "a" }, EnteredNodes(visits));
    }

    [TestMethod]
    public void Forward_PruneNodesAfter_KeepsTheNodeAsANewSink()
    {
      var pruned = DagWalkerCorpus.Chain().PruneNodesAfter(node => node == "b");

      using var dagnumerator = pruned.GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "a", "b" }, EnteredNodes(visits));
      Assert.IsFalse(visits.Any(visit => visit.Node == "c"), "c must not be discovered: b dispatches nothing.");
    }

    [TestMethod]
    public void Backward_PruneNodesBefore_RemovesTheNodeAndItsExclusiveAncestorSide()
    {
      // Backward = the transpose walked forward (order c, b, a): pruning b before entry kills
      // b, and a -- b's exclusive reach in this orientation -- dies with it.
      using var dagnumerator = DagWalkerCorpus.Chain().Transpose().PruneNodesBefore(node => node == "b").GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "c" }, EnteredNodes(visits));
    }

    [TestMethod]
    public void Backward_PruneNodesAfter_KeepsTheNodeAsANewSource()
    {
      // b enters, then dispatches nothing backward: a starves. In original orientation the
      // survivor is b->c, the mirror of the forward-after pin -- same predicate, opposite
      // half removed.
      using var dagnumerator = DagWalkerCorpus.Chain().Transpose().PruneNodesAfter(node => node == "b").GetDagnumerator();
      var visits = Drain(dagnumerator);

      CollectionAssert.AreEqual(new[] { "c", "b" }, EnteredNodes(visits));
      Assert.IsFalse(visits.Any(visit => visit.Node == "a"), "a must not be discovered: b dispatches nothing backward.");
    }

    [TestMethod]
    public void PruneNodesBefore_MaySplitTheDagIntoComponents_LegallyNotHazardously()
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

      var pruned = dag.PruneNodesBefore(node => node == "mid");

      using var dagnumerator = pruned.GetDagnumerator();
      var entered = EnteredNodes(Drain(dagnumerator));

      CollectionAssert.AreEquivalent(new[] { "alpha", "beta", "x", "y" }, entered);
    }
  }
}
