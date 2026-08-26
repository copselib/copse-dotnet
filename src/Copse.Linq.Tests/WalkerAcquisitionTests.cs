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
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

      // Order is deliberately unspecified by the contract; the SET is the promise.
      CollectionAssert.AreEquivalent(
        new[] { 0, 1, 2, 3, 4, 5, 6 },
        walkable.GetHandles().ToList());
    }

    [TestMethod]
    public void GetHandlesWithValues_YieldsTheRowsOfTheLabeling()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

      var rows = walkable.GetHandlesWithValues().ToDictionary(pair => pair.Handle, pair => pair.Node);

      Assert.AreEqual(7, rows.Count);
      Assert.AreEqual("a", rows[0]);
      Assert.AreEqual("d", rows[2]);
      Assert.AreEqual("c", rows[4]);
      Assert.AreEqual("g", rows[6]);
    }

    [TestMethod]
    public void TheCapstoneAcquisitionLine_RunsForReal()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

      // UC-32's line verbatim: the predicate is consumer code -- here "flagged" means d or g --
      // and the handles come back ready to jump in with.
      var targets = walkable
        .GetHandlesWithValues()
        .Where(pair => pair.Node == "d" || pair.Node == "g")
        .Select(pair => pair.Handle)
        .OrderBy(handle => handle)
        .ToList();

      CollectionAssert.AreEqual(new[] { 2, 6 }, targets);

      // The point of keeping handles: jump straight in -- the walker spelling (Stage B):
      // a stored handle becomes a stance again through a vantage already held (the jump),
      // and navigation speaks steps, never probes. d's parent is b; g's parent is c.
      var stance = walkable.GetTreeWalker();

      Assert.AreEqual("b", stance.At(targets[0]).MoveToParent().Value.GetValue());
      Assert.AreEqual("c", stance.At(targets[1]).MoveToParent().Value.GetValue());
    }

    [TestMethod]
    public void GetHandles_WalksForestsAndBothStoreFamilies()
    {
      // Forest through the escalation: a(b), c -- preorder handles a=0 b=1 c=2.
      var forest = TreeSerializer.DeserializeDepthFirstTree("a(b),c").Materialize(BufferLayout.Preorder);
      CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, forest.GetHandles().ToList());

      // The level-order capture serves the same scan through the same generic derivation --
      // different handle meanings (encoding-local), same value set.
      var levelOrder = TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)").Materialize(BufferLayout.LevelOrder);

      CollectionAssert.AreEquivalent(
        new[] { "a", "b", "c", "d", "e" },
        levelOrder.GetHandlesWithValues().Select(pair => pair.Node).ToList());
    }

    // The contract's door, total (walker factory design §11, the sentinel completion):
    // every citizen manufactures a walker at the UNFOCUSED STANCE with the topology bound at
    // birth -- the roots are its child group, and the empty forest is the unfocused stance
    // alone, an answer rather than a miss (its child group is empty).
    [TestMethod]
    public void TheDoor_EveryCitizen_AndTheEmptyForestAnswers()
    {
      foreach (var walkable in WalkerLawProviders.Walkables("a(b,c)"))
      {
        var door = walkable.GetTreeWalker();

        Assert.IsFalse(door.HasFocus, "the door stands at the unfocused stance, above the roots");
        Assert.AreEqual("a", door.MoveToChild(0).Value.GetValue(), "the first root is the unfocused stance's first child");
        Assert.AreEqual("b", door.MoveToChild(0).Value.MoveToChild(0).Value.GetValue(), "and the stance navigates");
      }

      var empty = TreeSerializer.DeserializeDepthFirstTree("a").Where(context => false).Materialize(BufferLayout.Preorder);
      var emptyDoor = empty.GetTreeWalker();

      Assert.IsFalse(emptyDoor.HasFocus, "the empty forest is the unfocused stance alone");
      Assert.IsFalse(emptyDoor.MoveToChild(0).HasValue, "with an empty child group -- the miss moved from the door to the boundary");
    }

    // The search law (naming grammar, 2026-08-14): searches are not surface. FindHandles and
    // the result-typed FindHandle were retired the day they were reviewed -- both were
    // GetHandlesWithValues plus consumer LINQ, the "do our thing, then call LINQ" shape the
    // surface refuses. A search's honest miss is the EMPTY SEQUENCE; downstream result-typed
    // consumers (SpanningSubtree of an empty search) carry the miss without a singular wrapper.
    [TestMethod]
    public void SearchesAreConsumerLinq_AndTheEmptySequenceIsTheMiss()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize(BufferLayout.Preorder);

      // The rowid idiom, spelled honestly: rows in, value predicate, handles out.
      var hits = walkable.GetHandlesWithValues()
        .Where(row => row.Node == "d" || row.Node == "c")
        .Select(row => row.Handle)
        .ToList();

      CollectionAssert.AreEquivalent(
        new[] { "d", "c" },
        hits.Select(handle => WalkerLawProviders.TopologyOf(walkable).GetValue(handle)).ToList());

      // A missed search is an empty sequence -- the miss, spoken natively.
      Assert.AreEqual(0, walkable.GetHandlesWithValues().Count(row => row.Node == "zzz"));

      // THE SENTINEL TRAP, pinned as a warning: FirstOrDefault on a missed search returns
      // default(int) = 0 -- a REAL handle (the first preorder node). Never FirstOrDefault
      // over ordinal handles; test emptiness, or flow the plural into a result-typed
      // consumer and let the miss stay typed.
      var trap = walkable.GetHandlesWithValues()
        .Where(row => row.Node == "zzz")
        .Select(row => row.Handle)
        .FirstOrDefault();
      Assert.AreEqual(0, trap);
      Assert.AreEqual("a", WalkerLawProviders.TopologyOf(walkable).GetValue(trap), "the miss masquerades as the root");
    }
  }
}
