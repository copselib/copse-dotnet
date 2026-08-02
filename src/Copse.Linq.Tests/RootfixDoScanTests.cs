using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure rootfix scan (SPIKE, feature/do-scan): the Do idiom, scan-shaped. The battery
  // pins the operator's two contracts -- compute pure/permissive, store exactly once per node
  // per traversal -- plus the pass-through (the result IS the source tree), the documented
  // per-traversal refire, and Memoize/Materialize as the pinning composition.
  [TestClass]
  public class RootfixDoScanTests
  {
    private sealed class Entity
    {
      public int Amount;
      public int Total;
      public int Stores;

      public override string ToString() => $"{Amount}";
    }

    // The toy workload: a-10(b-5,c-0,d-1) as mutable entities, path totals seeded at 100.
    // Materialize pins ONE set of entity instances so effects land on stable objects and
    // every drain replays the same references (a live deserialize would mint fresh entities
    // per traversal -- the mutable workload wants an owned tree).
    private static ITreenumerableBuffer<Entity> Structure() =>
      TreeSerializer
        .DeserializeDepthFirstTree("10(5,0,1)", (string s) => new Entity { Amount = int.Parse(s) })
        .Materialize();

    private static ITreenumerable<Entity> DoScan(ITreenumerable<Entity> tree) =>
      tree.RootfixDoScan(
        100,
        (arrived, entity) => arrived + entity.Amount,
        (entity, total) => { entity.Total = total; entity.Stores++; });

    [TestMethod]
    public void TotalsLandOnTheEntities_StoreFiresOncePerNode()
    {
      var tree = Structure();

      DoScan(tree).PreorderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Stores).ToArray());
    }

    [TestMethod]
    public void NodesPassThroughUnchanged_BothDimensions()
    {
      var tree = Structure();
      var doScan = DoScan(tree);

      CollectionAssert.AreEqual(tree.PreorderTraversal().ToArray(), doScan.PreorderTraversal().ToArray());
      CollectionAssert.AreEqual(tree.LevelOrderTraversal().ToArray(), doScan.LevelOrderTraversal().ToArray());
    }

    [TestMethod]
    public void EffectsFirePerTraversal_TheDocumentedRefire()
    {
      var tree = Structure();
      var doScan = DoScan(tree);

      doScan.PreorderTraversal().ToArray();
      doScan.LevelOrderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 2, 2, 2, 2 }, entities.Select(e => e.Stores).ToArray());

      // The compute/store split's payoff: inputs (Amount) and outputs (Total) are distinct
      // fields, so the refire is idempotent on the totals -- re-running is harmless by shape.
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void MaterializePinsTheEffects()
    {
      var tree = Structure();

      var pinned = DoScan(tree).Materialize();

      pinned.PreorderTraversal().ToArray();
      pinned.LevelOrderTraversal().ToArray();
      pinned.PreorderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Stores).ToArray(),
        "one drain at the capture; replays never re-fire");
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void MemoizePinsTheEffects()
    {
      var tree = Structure();

      using var pinned = DoScan(tree).Memoize();
      pinned.Complete();

      pinned.PreorderTraversal().ToArray();
      pinned.LevelOrderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Stores).ToArray());
    }

    [TestMethod]
    public void BreadthFirstDrain_StoreStillFiresOncePerNode()
    {
      var tree = Structure();

      DoScan(tree).LevelOrderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Stores).ToArray());
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void ForestRootsShareTheSeed()
    {
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("10(5),20(7)", (string s) => new Entity { Amount = int.Parse(s) })
        .Materialize();

      forest
        .RootfixDoScan(100, (arrived, entity) => arrived + entity.Amount, (entity, total) => entity.Total = total)
        .PreorderTraversal()
        .ToArray();

      CollectionAssert.AreEqual(
        new[] { 110, 115, 120, 127 },
        forest.PreorderTraversal().Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void RootSelector_SeedsPerRoot_AndComputeNeverSeesRoots()
    {
      // Under the selector form a root's accumulation IS the selector's value (the pure
      // scan's forest-correct clause, inherited): compute never runs at roots, so the root's
      // own Amount is deliberately absent from its Total.
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("10(5),20(7)", (string s) => new Entity { Amount = int.Parse(s) })
        .Materialize();

      forest
        .RootfixDoScan(
          root => root.Amount * 100,
          (arrived, entity) => arrived + entity.Amount,
          (entity, total) => { entity.Total = total; entity.Stores++; })
        .PreorderTraversal()
        .ToArray();

      var entities = forest.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1000, 1005, 2000, 2007 }, entities.Select(e => e.Total).ToArray());
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Stores).ToArray(),
        "roots are stored via the selector wrapper, non-roots via the accumulator -- once each");
    }

    [TestMethod]
    public void SelectorForm_ReadsLiveState_PerDrain_TheFreshnessRule()
    {
      // Seed-semantics-follow-purity, pinned: a seed VALUE is frozen at the call site, but the
      // selector fires during each traversal, so a closure over a mutated local is read fresh
      // per drain -- the Do tier's sanctioned per-run boundary input.
      var tree = Structure();
      var budget = 100;

      var doScan = tree.RootfixDoScan(
        _ => budget,
        (arrived, entity) => arrived + entity.Amount,
        (entity, total) => entity.Total = total);

      doScan.PreorderTraversal().ToArray();
      var firstDrain = tree.PreorderTraversal().Select(e => e.Total).ToArray();

      budget = 200;
      doScan.PreorderTraversal().ToArray();
      var secondDrain = tree.PreorderTraversal().Select(e => e.Total).ToArray();

      // Selector-form roots take the selector's value directly (compute never runs at roots):
      // root Total = budget itself, children = budget + own path amounts.
      CollectionAssert.AreEqual(new[] { 100, 105, 100, 101 }, firstDrain);
      CollectionAssert.AreEqual(new[] { 200, 205, 200, 201 }, secondDrain,
        "the second traversal read the mutated budget -- closures capture variables, not copies");
    }
  }
}
