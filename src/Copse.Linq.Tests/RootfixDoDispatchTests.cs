using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure survey pass: RootfixDispatch's Do twin. The battery pins the (survey, store)
  // contracts -- ONE subject-less dispatcher for every family (the virtual forest root's
  // first, full participation 2026-08-04), survey pure over the slot protocol, store exactly
  // once per node PER BUILD (deferred-once: effects at first drain, replays never re-fire) --
  // plus pass-through, total coverage, the work-shaped weighted allocator, and the inherited
  // slot strictness.
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

    // a carries weight 1 (the virtual family's split over a sole root hands it the whole
    // budget); b/c split a's arrival 75/25; d/e split b's 50/50.
    private static ITreenumerableBuffer<Entity> Structure() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-1(b-3(d-1,e-1),c-1)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

    // The work-shaped survey, subject-less (the unified signature): allocate the family's
    // arrival pro rata by member weight. The SAME callback serves the virtual root family
    // and every internal family -- one dispatcher, no duplication.
    private static void AllocateByWeight(decimal arrival, DispatchTargets<Entity, decimal> members)
    {
      var totalWeight = 0m;
      foreach (var member in members)
        totalWeight += member.Node.Weight;

      foreach (var member in members)
        member.Dispatch(arrival * member.Node.Weight / totalWeight);
    }

    private static ITreenumerableBuffer<Entity> Allocated(ITreenumerable<Entity> tree) =>
      tree.RootfixDoDispatch(
        10_000m,
        AllocateByWeight,
        (entity, arrived) => { entity.Received = arrived; entity.Stores++; });

    [TestMethod]
    public void AmountsLandOnTheEntities_RootsParticipateLikeEveryLevel()
    {
      var tree = Structure();

      Allocated(tree).PreorderTraversal().ToArray();

      var byName = tree.PreorderTraversal().ToDictionary(e => e.Name);
      Assert.AreEqual(10_000m, byName["a"].Received, "the virtual family's split over a sole root: the whole budget");
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
      // The selector flavors remain the boundary's sugar for roots that follow a DIFFERENT,
      // per-root rule than the survey -- here, seeding by root ordinal.
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
          (arrival, members) => members[0].Dispatch(arrival),  // ignores the rest
          (entity, arrived) => { });

      Assert.ThrowsException<InvalidOperationException>(
        () => allocated.PreorderTraversal().ToArray(),
        "the pure operator's exactly-once slot validation rides the shared pass");
    }

    [TestMethod]
    public void SeedFlavor_OneDispatcherAllocatesAcrossTheForestRoots()
    {
      // Full participation, unified (2026-08-04), the day-job shape: ONE budget split ACROSS
      // the forest's roots pro rata by weight, then onward down each tree -- and it is the
      // SAME AllocateByWeight at every family, the virtual root's included. No root-specific
      // callback exists; the boundary is an invocation, not a callback.
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("a-1(b-3,c-1),d-3", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

      forest
        .RootfixDoDispatch(8_000m, AllocateByWeight, (entity, arrived) => entity.Received = arrived)
        .PreorderTraversal()
        .ToArray();

      // The virtual family splits 8000 by 1:3 (a=2000, d=6000); a's children split 2000 by 3:1.
      CollectionAssert.AreEqual(
        new[] { 2_000m, 1_500m, 500m, 6_000m },
        forest.PreorderTraversal().Select(e => e.Received).ToArray());
    }
  }
}
