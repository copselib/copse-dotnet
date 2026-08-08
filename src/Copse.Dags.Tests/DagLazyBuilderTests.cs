using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // THE LAZY BUILDER RULING (2026-08-06, docs/DAG_CONTRACT_DESIGN.md): builder acquisition is
  // Kahn on demand -- no snapshot, no cycle check. A cyclic graph streams its maximal acyclic
  // downward-closed prefix and throws DagCycleException at the starvation point,
  // deterministically per drain; Materialize is the validator (a completed buffer is the
  // certificate); early-exiting consumers never pay for -- or learn about -- the cycle, the
  // same way a lazy tree's infinity is only discovered by not finishing.
  [TestClass]
  public class DagLazyBuilderTests
  {
    // s -> a -> b -> a: a two-node cycle hanging under an honest source.
    private static Dag<string, int> CycleUnderSource()
    {
      var s = new DagNode<string, int>("s");
      var a = s.AddChild("a");
      var b = a.AddChild("b");
      b.AddChild(a);
      return new Dag<string, int>(s);
    }

    [TestMethod]
    public void CyclicGraph_StreamsTheAcyclicPrefix_ThenStarves()
    {
      using var walk = CycleUnderSource().GetDagnumerator();

      // The prefix: s's conventional discovery, s's entry, s's dispatch of a. a never settles
      // (b's edge is undeliverable), so the next advance starves.
      Assert.IsTrue(walk.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(DagnumeratorMode.DiscoveringNode, walk.Mode);
      Assert.AreEqual("s", walk.Node);

      Assert.IsTrue(walk.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(DagnumeratorMode.EnteringNode, walk.Mode);
      Assert.AreEqual("s", walk.Node);

      Assert.IsTrue(walk.MoveNext(DagTraversalStrategies.TraverseAll));
      Assert.AreEqual(DagnumeratorMode.DiscoveringNode, walk.Mode);
      Assert.AreEqual("a", walk.Node);

      var exception = Assert.ThrowsException<DagCycleException>(
        () => walk.MoveNext(DagTraversalStrategies.TraverseAll));
      StringAssert.Contains(exception.Message, "starved");
    }

    [TestMethod]
    public void Starvation_NamesAConcreteCyclePath()
    {
      // The starvation exception must be as actionable as the eager walk's: one concrete
      // loop named ("Cycle detected: a -> b -> a"), found by the failure-path-only DFS over
      // the starved members, with the starved count kept as context.
      var exception = Assert.ThrowsException<DagCycleException>(() => CycleUnderSource().Materialize());

      StringAssert.Contains(exception.Message, "Cycle detected: a -> b -> a");
      StringAssert.Contains(exception.Message, "starved");
    }

    [TestMethod]
    public void Starvation_NamesTheLoop_NotItsDownstreamVictims()
    {
      // s -> a -> b -> c -> b, plus c -> d: d starves too (its only in-path runs through
      // the loop) but is no part of it; the named path must be exactly the loop.
      var s = new DagNode<string, int>("s");
      var a = s.AddChild("a");
      var b = a.AddChild("b");
      var c = b.AddChild("c");
      c.AddChild(b);
      c.AddChild("d");

      var exception = Assert.ThrowsException<DagCycleException>(() => new Dag<string, int>(s).Materialize());

      StringAssert.Contains(exception.Message, "Cycle detected: b -> c -> b");
      StringAssert.Contains(exception.Message, "3 node(s) starved");
    }

    [TestMethod]
    public void CyclicGraph_PrefixIsDeterministic_PerDrain()
    {
      var dag = CycleUnderSource();

      for (var drain = 0; drain < 2; drain++)
      {
        using var walk = dag.GetDagnumerator();
        var prefix = 0;

        try
        {
          while (walk.MoveNext(DagTraversalStrategies.TraverseAll))
            prefix++;
          Assert.Fail("expected starvation");
        }
        catch (DagCycleException)
        {
          Assert.AreEqual(3, prefix, $"drain {drain}: same prefix every drain");
        }
      }
    }

    [TestMethod]
    public void CyclicGraph_EarlyExitingConsumers_NeverLearn()
    {
      // GetSources reads the sources-at-the-start prefix and exits before the starvation
      // point -- a cyclic graph's sources stream fine.
      CollectionAssert.AreEqual(new[] { "s" }, CycleUnderSource().GetSources().ToArray());
    }

    [TestMethod]
    public void Materialize_IsTheValidator()
    {
      // The full drain: a cyclic source throws from inside the capture; an acyclic one
      // completes, and the completed buffer IS the acyclicity certificate.
      Assert.ThrowsException<DagCycleException>(() => CycleUnderSource().Materialize());

      var s = new DagNode<string, int>("s");
      s.AddChild("a").AddChild("b");
      var certificate = new Dag<string, int>(s).Materialize();

      Assert.AreEqual(3, certificate.Count);
    }

    [TestMethod]
    public void GetTopologicalOrder_DrainValidates()
    {
      Assert.ThrowsException<DagCycleException>(
        () => ((IDagnumerable<string, int>)CycleUnderSource()).GetTopologicalOrder());
    }

    [TestMethod]
    public void MutateBetweenDrains_TheNextAcquisitionSeesTheNewShape()
    {
      // The re-enumeration contract: "is acyclic" is a predicate of a drain, not of the
      // mutable builder -- each acquisition walks the graph as it is then.
      var root = new DagNode<string, int>("root");
      root.AddChild("a");
      var dag = new Dag<string, int>(root);

      CollectionAssert.AreEqual(new[] { "root", "a" }, dag.GetTopologicalOrder().Select(n => n.Value).ToArray());

      root.AddChild("b");

      CollectionAssert.AreEqual(
        new[] { "root", "a", "b" },
        ((IDagnumerable<string, int>)dag).GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void AcyclicStreams_AreUnchangedByTheLazyWalk()
    {
      // The ownership diamond, drained: same visits, same ordinals as the eager walk always
      // pinned (the wider battery holds the exhaustive stream pins; this is the smoke).
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild("left", 0.60m);
      var right = apex.AddChild("right", 0.40m);
      var venture = new DagNode<string, decimal>("venture");
      left.AddChild(venture, 0.70m);
      right.AddChild(venture, 0.30m);

      CollectionAssert.AreEqual(
        new[] { "apex", "left", "right", "venture" },
        new Dag<string, decimal>(apex).GetTopologicalOrder().Select(n => n.Value).ToArray());
    }
  }
}
