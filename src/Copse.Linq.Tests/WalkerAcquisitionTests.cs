using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The rowid scan's pins: handle-space enumerated (never computed from values), the pair rows
  // bridging value-space to handle-space, and the capstone's acquisition line run for real --
  // predicates are consumer code, equality in no signature (WALKER_USE_CASES.md UC-32).
  [TestClass]
  public class WalkerAcquisitionTests
  {
    // a(b(d,e),c(f,g)) -- preorder handles: a=0 b=1 d=2 e=3 c=4 f=5 g=6.
    private const string ToyTree = "a(b(d,e),c(f,g))";

    [TestMethod]
    public void GetHandles_YieldsEveryHandleExactlyOnce()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      // Order is deliberately unspecified by the contract; the SET is the promise.
      CollectionAssert.AreEquivalent(
        new[] { 0, 1, 2, 3, 4, 5, 6 },
        walkable.GetHandles().ToList());
    }

    [TestMethod]
    public void GetHandlesWithValues_YieldsTheRowsOfTheLabeling()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      var rows = walkable.GetHandlesWithValues().ToDictionary(pair => pair.Handle, pair => pair.Value);

      Assert.AreEqual(7, rows.Count);
      Assert.AreEqual("a", rows[0]);
      Assert.AreEqual("d", rows[2]);
      Assert.AreEqual("c", rows[4]);
      Assert.AreEqual("g", rows[6]);
    }

    [TestMethod]
    public void TheCapstoneAcquisitionLine_RunsForReal()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      // UC-32's line verbatim: the predicate is consumer code -- here "flagged" means d or g --
      // and the handles come back ready to jump in with.
      var targets = walkable
        .GetHandlesWithValues()
        .Where(pair => pair.Value == "d" || pair.Value == "g")
        .Select(pair => pair.Handle)
        .OrderBy(handle => handle)
        .ToList();

      CollectionAssert.AreEqual(new[] { 2, 6 }, targets);

      // The point of keeping handles: jump straight in. d's parent is b; g's parent is c.
      Assert.AreEqual(1, walkable.GetParent(targets[0]).Parent);
      Assert.AreEqual(4, walkable.GetParent(targets[1]).Parent);
    }

    [TestMethod]
    public void GetHandles_WalksForestsAndBothStoreFamilies()
    {
      // Forest through the escalation: a(b), c -- preorder handles a=0 b=1 c=2.
      var forest = TreeSerializer.DeserializeDepthFirstTree("a(b),c").MaterializeWalkable();
      CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, forest.GetHandles().ToList());

      // The level-order walkable serves the same scan through the same generic derivation --
      // different handle meanings (encoding-local), same value set.
      var levelOrder = new WalkableLevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(
        new LevelOrderArrayStore<string>(
          ["a", "b", "e", "c", "d"],
          firstChildIndices: [1, 3, 0, 0, 0],
          childCounts: [2, 2, 0, 0, 0],
          rootCount: 1));

      CollectionAssert.AreEquivalent(
        new[] { "a", "b", "c", "d", "e" },
        levelOrder.GetHandlesWithValues().Select(pair => pair.Value).ToList());
    }
  }
}
