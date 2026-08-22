using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The sentinel completion's acceptance pins, dag-side (WALKER_FACTORY_DESIGN.md §11's mapping
  // tables, dualized; the theory is CATEGORY_THEORY_SURVEY.md §12, carrier-neutral): the
  // unfocused stance is a walker STATE -- the VIRTUAL SOURCE, which the dag family had already
  // built as the seed's origin (the virtual source family): the door lands on it, a source's
  // climb tops out standing on it, the sources are its child group, and its own parent is the
  // algebra's one upward miss. Value reads exclude it BY TYPE: GetValue and Focus throw (the
  // violation channel), TryGetValue misses, and the hoist (Downstream()) carries a row per stance
  // exactly when the stance has a value. Run over the empty dag, the two islands, and the diamond.
  [TestClass]
  public class DagUnfocusedStanceTests
  {
    [TestMethod]
    public void TheDoorIsTotal_AllThreeDags()
    {
      foreach (var factory in new Func<Dag<string, decimal>>[] { DagWalkerCorpus.Empty, DagWalkerCorpus.TwoIslands, DagWalkerCorpus.Diamond })
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          Assert.IsFalse(walkable.GetDagWalker().HasFocus, $"every door lands on the unfocused stance -- the empty dag included [{name}]");

      Assert.IsFalse(DagWalkerCorpus.Empty().GetDagWalker().HasFocus, "builder");
      Assert.IsFalse(new FamilyFreeDag().GetDagWalker().HasFocus, "foreign");
    }

    [TestMethod]
    public void RoundTrip_DoorThenHoist_IsTheSource()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          Assert.AreEqual(
            DagWalkerCorpus.Content(walkable),
            DagWalkerCorpus.Content(walkable.GetDagWalker().Downstream()),
            $"door-then-hoist is the identity, with no case analysis [{dagName}/{name}]");

      Assert.AreEqual(DagWalkerCorpus.Content(DagWalkerCorpus.SharedLeaf()), DagWalkerCorpus.Content(DagWalkerCorpus.SharedLeaf().GetDagWalker().Downstream()), "builder");
    }

    [TestMethod]
    public void StanceTable_TheIslands()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.TwoIslands))
      {
        var unfocused = walkable.GetDagWalker();
        Assert.IsFalse(unfocused.TryGetValue(out _), $"the unfocused stance has no value -- the typed miss [{name}]");
        Assert.AreEqual(
          "nodes[island1,island1Child,island2] edges[island1->island1Child:1.00] sources[island1,island2]",
          DagWalkerCorpus.Content(unfocused.Downstream()),
          $"hoist at the unfocused stance: the whole dag, every island [{name}]");

        var island1 = unfocused.MoveToChild(0).Value;
        var island2 = unfocused.MoveToChild(1).Value;
        Assert.AreEqual("island1", island1.GetValue(), name);
        Assert.IsTrue(island2.TryGetValue(out var island2Value) && island2Value == "island2", $"interior TryGetValue agrees with GetValue [{name}]");
        Assert.IsFalse(unfocused.MoveToChild(2).HasValue, $"past the last source [{name}]");
        Assert.AreEqual("nodes[island1,island1Child] edges[island1->island1Child:1.00] sources[island1]", DagWalkerCorpus.Content(island1.Downstream()), $"hoist at a source: its island, single-sourced [{name}]");
        Assert.AreEqual("nodes[island2] edges[] sources[island2]", DagWalkerCorpus.Content(island2.Downstream()), $"no stance hoists to a SIBLING island -- only the unfocused stance reaches them all [{name}]");
      }
    }

    [TestMethod]
    public void StanceTable_TheDiamond()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var door = walkable.GetDagWalker();
        Assert.AreEqual(DagWalkerCorpus.Content(walkable), DagWalkerCorpus.Content(door.Downstream()), $"hoist at the unfocused stance: the whole diamond [{name}]");
        var left = door.MoveToChild(0).Value.MoveToChild(0).Value;
        Assert.AreEqual("nodes[left,venture] edges[left->venture:0.70] sources[left]", DagWalkerCorpus.Content(left.Downstream()), $"hoist at left: its cone, re-sourced -- the unfocused row was never a special case [{name}]");
        Assert.AreEqual(0.60m, door.MoveToChild(0).Value.MoveToChild(0).Edge, $"the edge crossed [{name}]");
      }
    }

    [TestMethod]
    public void TheClimb_FromTheVenture_AnswersToTheVirtualSourceThenMisses()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var stance = walkable.GetDagWalkerAt(walkable.GetHandlesWithValues().Single(row => row.Value == "venture").Handle);
        var ancestors = new List<string>();

        // The climb idiom along in-edge 0: step up while the step answers, then test HasFocus.
        while (stance.MoveToParent(0).TryGetValue(out stance) && stance.HasFocus)
          ancestors.Add(stance.GetValue());

        CollectionAssert.AreEqual(new[] { "left", "apex" }, ancestors, $"the in-edge-0 path to the source [{name}]");
        Assert.IsFalse(stance.HasFocus, $"the climb tops out standing on the virtual source [{name}]");
        Assert.IsFalse(stance.MoveToParent(0).HasValue, $"stepping up from the unfocused stance is the one upward miss [{name}]");

        // Along in-edge 1 instead: the other path.
        var viaRight = walkable.GetDagWalkerAt(walkable.GetHandlesWithValues().Single(row => row.Value == "venture").Handle).MoveToParent(1).Value;
        Assert.AreEqual("right", viaRight.GetValue(), name);
        Assert.AreEqual("apex", viaRight.MoveToParent(0).Value.GetValue(), name);
        Assert.IsFalse(viaRight.MoveToParent(1).HasValue, $"a single-parent node's index-1 step is a plain miss, not the virtual source [{name}]");
        Assert.IsFalse(viaRight.MoveToParent(0).Value.MoveToParent(1).HasValue, $"a source's index-1 step is a plain miss [{name}]");
        Assert.IsFalse(viaRight.MoveToParent(0).Value.MoveToParent(0).Value.HasFocus, $"a source's index-0 step is the virtual source [{name}]");
      }
    }

    [TestMethod]
    public void TheViolationChannel_AndTheTypedMiss()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var unfocused = walkable.GetDagWalker();
        Assert.ThrowsException<InvalidOperationException>(() => unfocused.GetValue(), $"GetValue at the unfocused stance throws [{name}]");
        Assert.ThrowsException<InvalidOperationException>(() => unfocused.Focus, $"Focus at the unfocused stance throws [{name}]");
        Assert.IsFalse(unfocused.TryGetValue(out _), $"TryGetValue misses [{name}]");
        Assert.AreEqual("unfocused walker", unfocused.ToString(), name);
      }
    }

    [TestMethod]
    public void TheCompletedExtend_InteriorsByExtend_RootRowByDirectApplication()
    {
      // extract ∘ extend = f at every stance, the unfocused one included: the observer counts
      // the nodes in the stance's hoist -- at the unfocused stance, the whole dag.
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        Func<DagWalker<string, int, decimal>, int> coneSize = walker => walker.Downstream().GetHandles().Count();
        var door = walkable.GetDagWalker();
        var extended = door.Extend(coneSize);

        Assert.IsFalse(extended.HasFocus, $"extend keeps the stance [{name}]");
        Assert.AreEqual(4, coneSize(door), $"the unfocused row: a direct application [{name}]");
        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(coneSize(walkable.GetDagWalkerAt(handle)), extended.At(handle).GetValue(), $"interior rows by extend [{name}]");

        var apexRow = extended.MoveToChild(0).Value;
        Assert.AreEqual(4, apexRow.GetValue(), $"apex's cone is the whole diamond [{name}]");
        Assert.AreEqual(2, apexRow.MoveToChild(0).Value.GetValue(), $"left's cone is left and the venture [{name}]");
      }
    }

    [TestMethod]
    public void TheTransposedUnfocusedStance_StandsOnTheVirtualSink()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
      {
        var transposedDoor = walkable.GetDagWalker().Transpose();
        Assert.IsFalse(transposedDoor.HasFocus, name);
        Assert.AreEqual("sharedLeaf", transposedDoor.MoveToChild(0).Value.GetValue(), $"its child group is the sinks [{name}]");
        Assert.IsFalse(transposedDoor.MoveToChild(1).HasValue, $"one sink [{name}]");
        Assert.AreEqual(DagWalkerCorpus.Content(walkable.Transpose()), DagWalkerCorpus.Content(transposedDoor.Downstream()), $"its hoist is the transposed dag [{name}]");
      }
    }
  }
}
