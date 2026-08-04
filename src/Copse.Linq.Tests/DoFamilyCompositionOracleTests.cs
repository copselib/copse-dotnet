using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // THE FAMILY EQUATION, pinned (ratified 2026-08-04, docs/SCANRESULT_DESIGN.md):
  //
  //   XDoY  ==  XY(pure) . Do(scheduling-filtered effect) . Select(.Node)
  //
  // Every Do operator is derivable from the pure tier plus Do plus Select -- the dedicated
  // operators are that composition's node-grained, effect-class-corrected fusions ("sugar +
  // license"). This battery is the Do family's conformance oracle: each test runs the
  // dedicated operator and the composition against two identical mutable corpora and asserts
  // identical landed state. The scheduling-mode filter in every composed effect is the trap
  // the dedicated operators exist to bury: Do fires per VISIT EVENT (a k-child node emits
  // 1 S + k+1 V), so an unfiltered effect multi-fires -- scheduling alone is once per node.
  [TestClass]
  public class DoFamilyCompositionOracleTests
  {
    private sealed class Entity
    {
      public string Name;
      public decimal Weight;
      public decimal Landed;

      public override string ToString() => Name;
    }

    // a-10(b-5(d-1,e-2),c-4): amounts on every node; two identical corpora per test so the
    // dedicated and composed passes mutate disjoint object graphs.
    private static ITreenumerableBuffer<Entity> Corpus() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-10(b-5(d-1,e-2),c-4)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

    private static decimal[] LandedPreorder(ITreenumerable<Entity> corpus) =>
      corpus.PreorderTraversal().Select(e => e.Landed).ToArray();

    [TestMethod]
    public void RootfixDoScan_EqualsScanDoSelect()
    {
      var dedicated = Corpus();
      var composed = Corpus();

      dedicated
        .RootfixDoScan(100m, (arrived, e) => e.Landed = arrived + e.Weight)
        .PreorderTraversal().ToArray();

      composed
        .RootfixScan(100m, (arrived, e) => arrived + e.Weight)
        .Do(visit =>
        {
          if (visit.Mode == TreenumeratorMode.SchedulingNode)
            visit.Node.Node.Landed = visit.Node.Accumulate;
        })
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { 110m, 115m, 116m, 117m, 114m }, LandedPreorder(dedicated));
      CollectionAssert.AreEqual(LandedPreorder(dedicated), LandedPreorder(composed));
    }

    [TestMethod]
    public void LeaffixDoScan_EqualsScanDoSelect()
    {
      var dedicated = Corpus();
      var composed = Corpus();

      dedicated
        .LeaffixDoScan(e => e.Weight, (accumulate, child) => accumulate + child, (e, total) => e.Landed = total)
        .PreorderTraversal().ToArray();

      composed
        .LeaffixScan(e => e.Weight, (accumulate, child) => accumulate + child)
        .Do(visit =>
        {
          if (visit.Mode == TreenumeratorMode.SchedulingNode)
            visit.Node.Node.Landed = visit.Node.Accumulate;
        })
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      // Subtree sums: d=1, e=2, b=5+1+2=8, c=4, a=10+8+4=22.
      CollectionAssert.AreEqual(new[] { 22m, 8m, 1m, 2m, 4m }, LandedPreorder(dedicated));
      CollectionAssert.AreEqual(LandedPreorder(dedicated), LandedPreorder(composed));
    }

    // The work-shaped survey, shared verbatim by the dedicated and composed passes (the seat
    // rule's callbacks-lift-unchanged clause): allocate the arrival pro rata by child weight.
    private static void AllocateByWeight(Entity parent, decimal arrival, DispatchTargets<Entity, decimal> children)
    {
      var totalWeight = 0m;
      foreach (var child in children)
        totalWeight += child.Node.Weight;

      foreach (var child in children)
        child.Dispatch(arrival * child.Node.Weight / totalWeight);
    }

    [TestMethod]
    public void RootfixDoDispatch_EqualsDispatchDoSelect()
    {
      var dedicated = Corpus();
      var composed = Corpus();

      dedicated
        .RootfixDoDispatch(9_000m, AllocateByWeight, (e, arrived) => e.Landed = arrived)
        .PreorderTraversal().ToArray();

      composed
        .RootfixDispatch(9_000m, AllocateByWeight)
        .Do(visit =>
        {
          if (visit.Mode == TreenumeratorMode.SchedulingNode)
            visit.Node.Node.Landed = visit.Node.Accumulate;
        })
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      // a gets the seed; b/c split 9000 by 5:4 (5000/4000); d/e split b's 5000 by 1:2.
      CollectionAssert.AreEqual(
        new[] { 9_000m, 5_000m, 5_000m / 3, 5_000m * 2 / 3, 4_000m }, LandedPreorder(dedicated));
      CollectionAssert.AreEqual(LandedPreorder(dedicated), LandedPreorder(composed));
    }

    // The upward survey, shared verbatim: a node's accumulation is its weight plus its
    // children's accumulations, read through the sibling-complete sources view.
    private static decimal RollUp(Entity node, DispatchSources<Entity, decimal> children)
    {
      var total = node.Weight;
      foreach (var child in children)
        total += child.Accumulate;

      return total;
    }

    [TestMethod]
    public void LeaffixDoDispatch_EqualsDispatchDoSelect()
    {
      var dedicated = Corpus();
      var composed = Corpus();

      dedicated
        .LeaffixDoDispatch(1m, RollUp, (e, accumulate) => e.Landed = accumulate)
        .PreorderTraversal().ToArray();

      composed
        .LeaffixDispatch(1m, RollUp)
        .Do(visit =>
        {
          if (visit.Mode == TreenumeratorMode.SchedulingNode)
            visit.Node.Node.Landed = visit.Node.Accumulate;
        })
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      // Seed flavor: every LEAF takes the seed (1), internal nodes roll up:
      // d=1, e=1, b=5+1+1=7, c=1 (leaf), a=10+7+1=18.
      CollectionAssert.AreEqual(new[] { 18m, 7m, 1m, 1m, 1m }, LandedPreorder(dedicated));
      CollectionAssert.AreEqual(LandedPreorder(dedicated), LandedPreorder(composed));
    }
  }
}
