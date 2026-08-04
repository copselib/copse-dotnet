using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure survey pass (SPIKE, feature/do-scan): RootfixDispatch's Do twin. The battery
  // pins the (survey, store) contracts -- survey pure over the slot protocol, store exactly
  // once per node PER BUILD (deferred-once: effects at first drain, replays never re-fire) --
  // plus pass-through, total coverage (roots get the seed, leaves their arrivals), the
  // work-shaped weighted allocator, and the inherited slot strictness.
  [TestClass]
  public class RootfixDoDispatchTests
  {
    private sealed class Entity
    {
      public string Name;
      public decimal Weight;
      public decimal Received;
      public int Stores;

      public override string ToString() => Name;
    }

    // a(b,c) with weights: b carries 3, c carries 1 -- a 75/25 split of whatever arrives at a.
    private static ITreenumerableBuffer<Entity> Structure() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-0(b-3(d-1,e-1),c-1)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

    // The work-shaped survey: allocate the arrival pro rata by child weight.
    private static void AllocateByWeight(Entity parent, decimal arrival, DispatchTargets<Entity, decimal> children)
    {
      var totalWeight = 0m;
      foreach (var child in children)
        totalWeight += child.Node.Weight;

      foreach (var child in children)
        child.Dispatch(arrival * child.Node.Weight / totalWeight);
    }

    private static ITreenumerableBuffer<Entity> Allocated(ITreenumerable<Entity> tree) =>
      tree.RootfixDoDispatch(
        10_000m,
        AllocateByWeight,
        (entity, arrived) => { entity.Received = arrived; entity.Stores++; });

    [TestMethod]
    public void AmountsLandOnTheEntities_RootsSeedLeavesArrivals()
    {
      var tree = Structure();

      Allocated(tree).PreorderTraversal().ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(10_000m, byName["a"].Received, "the root's arrival IS the seed");
      Assert.AreEqual(7_500m, byName["b"].Received, "75% by weight");
      Assert.AreEqual(2_500m, byName["c"].Received, "25% by weight");
      Assert.AreEqual(3_750m, byName["d"].Received, "b's arrival split 50/50");
      Assert.AreEqual(3_750m, byName["e"].Received);

      CollectionAssert.AreEqual(
        new[] { 1, 1, 1, 1, 1 },
        tree.PreorderTraversal().Select(e => e.Stores).ToArray(),
        "store fires exactly once per node -- roots and leaves included");
    }

    [TestMethod]
    public void EffectsAreDeferredToTheFirstDrain_AndNeverRefire()
    {
      var tree = Structure();

      var allocated = Allocated(tree);
      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 0),
        "deferred-once: no effects before the first acquisition");

      allocated.PreorderTraversal().ToArray();
      allocated.LevelOrderTraversal().ToArray();
      allocated.PreorderTraversal().ToArray();

      Assert.IsTrue(tree.PreorderTraversal().All(e => e.Stores == 1),
        "the build ran once; replays never re-fire the effects");
    }

    [TestMethod]
    public void NodesPassThroughUnchanged_BothDimensions()
    {
      var tree = Structure();
      var allocated = Allocated(tree);

      CollectionAssert.AreEqual(tree.PreorderTraversal().ToArray(), allocated.PreorderTraversal().ToArray());
      CollectionAssert.AreEqual(tree.LevelOrderTraversal().ToArray(), allocated.LevelOrderTraversal().ToArray());
    }

    [TestMethod]
    public void StoreRunsInPreorder_TheDocumentedOrder()
    {
      var tree = Structure();
      var storeOrder = new List<string>();

      tree
        .RootfixDoDispatch(0m, AllocateByWeight, (entity, _) => storeOrder.Add(entity.Name))
        .PreorderTraversal()
        .ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b", "d", "e", "c" }, storeOrder);
    }

    [TestMethod]
    public void PositionalSelector_SeedsForestRootsIndependently()
    {
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("r-0(x-1),s-0(y-1)", (string t) =>
        {
          var parts = t.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

      forest
        .RootfixDoDispatch(
          (root, position) => position.SiblingIndex == 0 ? 1_000m : 500m,
          AllocateByWeight,
          (entity, arrived) => entity.Received = arrived)
        .PreorderTraversal()
        .ToArray();

      var byName = forest.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(1_000m, byName["r"].Received);
      Assert.AreEqual(1_000m, byName["x"].Received, "the only child receives the whole arrival");
      Assert.AreEqual(500m, byName["s"].Received);
      Assert.AreEqual(500m, byName["y"].Received);
    }

    [TestMethod]
    public void SlotStrictnessIsInherited_AMissedChildThrowsAtFirstDrain()
    {
      var allocated = Structure()
        .RootfixDoDispatch(
          10_000m,
          (parent, arrival, children) => children[0].Dispatch(arrival),  // ignores the rest
          (entity, arrived) => { });

      Assert.ThrowsException<InvalidOperationException>(
        () => allocated.PreorderTraversal().ToArray(),
        "the pure operator's exactly-once slot validation rides the shared pass");
    }

    [TestMethod]
    public void RootSurvey_AllocatesTheBudgetAcrossTheForest()
    {
      // Full participation, the day-job shape: ONE budget split ACROSS the forest's roots pro
      // rata by weight, then onward down each tree -- in-band, sibling-complete, one pass; the
      // per-root selector could only seed each root in isolation.
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("a-1(b-3,c-1),d-3", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

      forest
        .RootfixDoDispatch(
          8_000m,
          (seed, roots) =>
          {
            var totalWeight = 0m;
            foreach (var root in roots)
              totalWeight += root.Node.Weight;

            foreach (var root in roots)
              root.Dispatch(seed * root.Node.Weight / totalWeight);
          },
          AllocateByWeight,
          (entity, arrived) => entity.Received = arrived)
        .PreorderTraversal()
        .ToArray();

      // Roots split 8000 by 1:3 (a=2000, d=6000); a's children split 2000 by 3:1.
      CollectionAssert.AreEqual(
        new[] { 2_000m, 1_500m, 500m, 6_000m },
        forest.PreorderTraversal().Select(e => e.Received).ToArray());
    }
  }
}
