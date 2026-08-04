using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The impure rootfix scan: the Do idiom, scan-shaped, MERGED (2026-08-04) -- one impure
  // fold whose return both lands on the node and flows to its children. The battery pins the
  // license (the fold fires exactly once per node per traversal), the pass-through (the
  // result IS the source tree), the documented per-traversal refire, the selector-lands-the-
  // roots clause, and Memoize/Materialize as the pinning composition.
  [TestClass]
  public class RootfixDoScanTests
  {
    private sealed class Entity
    {
      public int Amount;
      public int Total;
      public int Folds;

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

    // The doc-first block form: mutate, then return -- landing and flow as two visible acts.
    private static ITreenumerable<Entity> DoScan(ITreenumerable<Entity> tree) =>
      tree.RootfixDoScan(
        100,
        (arrived, entity) =>
        {
          entity.Total = arrived + entity.Amount;
          entity.Folds++;
          return entity.Total;
        });

    [TestMethod]
    public void TotalsLandOnTheEntities_FoldFiresOncePerNode()
    {
      var tree = Structure();

      DoScan(tree).PreorderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Folds).ToArray());
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
      CollectionAssert.AreEqual(new[] { 2, 2, 2, 2 }, entities.Select(e => e.Folds).ToArray());

      // The landing rule's re-runnable idiom: read fields (Amount) and written fields (Total)
      // are distinct, so the refire is idempotent on the totals -- unlike read-modify-write
      // (Total += arrived), which would compound and is a shape a caller writes deliberately.
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
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Folds).ToArray(),
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
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Folds).ToArray());
    }

    [TestMethod]
    public void BreadthFirstDrain_FoldStillFiresOncePerNode()
    {
      var tree = Structure();

      DoScan(tree).LevelOrderTraversal().ToArray();

      var entities = tree.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, entities.Select(e => e.Folds).ToArray());
      CollectionAssert.AreEqual(new[] { 110, 115, 110, 111 }, entities.Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void ForestRootsShareTheSeed()
    {
      // The terse landing idiom: C# assignment is an expression returning the assigned value,
      // so this expression-bodied fold is the block form's exact equivalent.
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("10(5),20(7)", (string s) => new Entity { Amount = int.Parse(s) })
        .Materialize();

      forest
        .RootfixDoScan(100, (arrived, entity) => entity.Total = arrived + entity.Amount)
        .PreorderTraversal()
        .ToArray();

      CollectionAssert.AreEqual(
        new[] { 110, 115, 120, 127 },
        forest.PreorderTraversal().Select(e => e.Total).ToArray());
    }

    [TestMethod]
    public void RootSelector_LandsTheRoots_AndTheFoldNeverFiresAtRoots()
    {
      // Under the selector form a root's value IS the selector's return (the pure scan's
      // forest-correct clause, inherited), so THE SELECTOR IS THE ROOT'S LANDING: the fold
      // never runs at roots, and the root's own Amount is deliberately absent from its Total.
      var forest = TreeSerializer
        .DeserializeDepthFirstTree("10(5),20(7)", (string s) => new Entity { Amount = int.Parse(s) })
        .Materialize();

      forest
        .RootfixDoScan(
          root => root.Total = root.Amount * 100,
          (arrived, entity) =>
          {
            entity.Total = arrived + entity.Amount;
            entity.Folds++;
            return entity.Total;
          })
        .PreorderTraversal()
        .ToArray();

      var entities = forest.PreorderTraversal().ToArray();
      CollectionAssert.AreEqual(new[] { 1000, 1005, 2000, 2007 }, entities.Select(e => e.Total).ToArray());
      CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, entities.Select(e => e.Folds).ToArray(),
        "roots land via the selector's return, non-roots via the fold's -- once each");
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
        root => root.Total = budget,
        (arrived, entity) => entity.Total = arrived + entity.Amount);

      doScan.PreorderTraversal().ToArray();
      var firstDrain = tree.PreorderTraversal().Select(e => e.Total).ToArray();

      budget = 200;
      doScan.PreorderTraversal().ToArray();
      var secondDrain = tree.PreorderTraversal().Select(e => e.Total).ToArray();

      // Selector-form roots take the selector's value directly (the fold never runs at roots):
      // root Total = budget itself, children = budget + own path amounts.
      CollectionAssert.AreEqual(new[] { 100, 105, 100, 101 }, firstDrain);
      CollectionAssert.AreEqual(new[] { 200, 205, 200, 201 }, secondDrain,
        "the second traversal read the mutated budget -- closures capture variables, not copies");
    }
  }
}
