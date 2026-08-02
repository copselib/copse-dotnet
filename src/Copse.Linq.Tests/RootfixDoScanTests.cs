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
  }
}
