using Copse;
using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The sentinel completion's acceptance pins (design-docs/WALKER_FACTORY_DESIGN.md §11;
  // the theory is CATEGORY_THEORY_SURVEY.md §12): the worked mapping tables, run over
  // the three example forests -- the empty forest, the two-root forest a,b, and the tree
  // a(b(d,e),c). The unfocused stance is a walker STATE: the door lands on it, climbs top out
  // standing on it, the roots are its child group, and its own parent is the algebra's one
  // upward miss. Value reads exclude it BY TYPE, never by rule: GetValue and Focus throw
  // (the violation channel -- Current before the first MoveNext), TryGetValue misses, and
  // the hoist (Subtree()) carries a row per stance exactly when the stance has a value.
  [TestClass]
  public class UnfocusedStanceTests
  {
    private static ITreenumerableBuffer<string> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.Preorder);

    private static ITreenumerableBuffer<string> Empty()
      => TreeSerializer.DeserializeDepthFirstTree("a").Where(context => false).Materialize(BufferLayout.Preorder);

    // Structure-sensitive drain: schedule-order values WITH positions, so the forest a,b
    // and the tree a(b) cannot alias.
    private static List<string> Shape(ITreenumerable<string> tree)
    {
      var shape = new List<string>();

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            shape.Add($"{treenumerator.Node}@{treenumerator.Position.Depth}.{treenumerator.Position.SiblingIndex}");
      }

      return shape;
    }

    [TestMethod]
    public void TheDoorIsTotal_AllThreeForests()
    {
      foreach (var walkable in new[] { Empty(), W("a,b"), W("a(b(d,e),c)") })
        Assert.IsFalse(walkable.GetTreeWalker().HasFocus, "every door lands on the unfocused stance -- the empty forest included");
    }

    [TestMethod]
    public void RoundTrip_DoorThenHoist_IsTheSource()
    {
      foreach (var tree in new[] { "a,b", "a(b(d,e),c)", "a(b),c(d)" })
      {
        var walkable = W(tree);

        CollectionAssert.AreEqual(
          Shape(walkable),
          Shape(walkable.GetTreeWalker().Subtree()),
          $"door-then-hoist is the identity, with no case analysis [{tree}]");
      }

      Assert.AreEqual(0, Shape(Empty().GetTreeWalker().Subtree()).Count, "the empty forest round-trips to itself");
    }

    [TestMethod]
    public void StanceTable_TheForest()
    {
      var walkable = W("a,b");
      var unfocusedStance = walkable.GetTreeWalker();

      Assert.IsFalse(unfocusedStance.TryGetValue().HasValue, "the unfocused stance has no value -- the typed miss");
      CollectionAssert.AreEqual(
        new[] { "a@0.0", "b@0.1" },
        Shape(unfocusedStance.Subtree()),
        "hoist at the unfocused stance: the whole forest, never a sentinel-headed tree");

      var rootA = unfocusedStance.MoveToChild(0).Value;
      var rootB = unfocusedStance.MoveToChild(1).Value;

      Assert.AreEqual("a", rootA.GetValue());
      Assert.AreEqual("b", rootB.TryGetValue().Value, "interior TryGetValue agrees with GetValue");
      Assert.IsFalse(unfocusedStance.MoveToChild(2).HasValue, "past the last root");

      CollectionAssert.AreEqual(new[] { "a@0.0" }, Shape(rootA.Subtree()), "hoist at a node: the subtree, single-rooted");
      CollectionAssert.AreEqual(new[] { "b@0.0" }, Shape(rootB.Subtree()), "no stance hoists to a SIBLING's tree -- only the unfocused stance reaches them all");
    }

    [TestMethod]
    public void StanceTable_TheTree()
    {
      var walkable = W("a(b(d,e),c)");
      var door = walkable.GetTreeWalker();

      CollectionAssert.AreEqual(
        new[] { "a@0.0", "b@1.0", "d@2.0", "e@2.1", "c@1.1" },
        Shape(door.Subtree()),
        "hoist at the unfocused stance: the whole tree");

      var nodeB = door.MoveToChild(0).Value.MoveToChild(0).Value;

      CollectionAssert.AreEqual(
        new[] { "b@0.0", "d@1.0", "e@1.1" },
        Shape(nodeB.Subtree()),
        "hoist at b: b(d,e), re-rooted -- the unfocused row was never a special case");
    }

    [TestMethod]
    public void TheClimb_FromD_AnswersToTheVoidThenMisses()
    {
      var walkable = W("a(b(d,e),c)");
      var handleOfD = walkable.GetHandlesWithValues().Single(row => row.Node == "d").Handle;

      var stance = walkable.GetTreeWalkerAt(handleOfD);
      var ancestors = new List<string>();

      // The climb idiom: step up while the step answers, then test HasFocus.
      while (stance.MoveToParent().TryGetValue(out stance) && stance.HasFocus)
        ancestors.Add(stance.GetValue());

      CollectionAssert.AreEqual(new[] { "b", "a" }, ancestors, "the interior ancestors, in climb order");
      Assert.IsFalse(stance.HasFocus, "the third answer is the unfocused stance -- the climb tops out standing, not missing");
      Assert.IsFalse(stance.MoveToParent().HasValue, "stepping up from the unfocused stance is the algebra's one upward miss");
    }

    [TestMethod]
    public void TheViolationChannel_AndTheTypedMiss()
    {
      var unfocusedStance = W("a,b").GetTreeWalker();

      Assert.ThrowsException<InvalidOperationException>(() => { unfocusedStance.GetValue(); });
      Assert.ThrowsException<InvalidOperationException>(() => { _ = unfocusedStance.Focus; });
      Assert.IsFalse(unfocusedStance.TryGetValue().HasValue, "the lawful read: the miss is typed, exactly at the unfocused stance");
    }

    [TestMethod]
    public void TheCompletedExtend_InteriorsByExtend_RootRowByDirectApplication()
    {
      // extract∘extend = f at every stance: shipped Extend covers the interiors and keeps
      // the stance (the unfocused stance included); its row is a direct application -- observing
      // ONE stance is a function call, never an operator (CATEGORY_THEORY_SURVEY.md §12).
      var walkable = W("a(b(d,e),c)");
      var door = walkable.GetTreeWalker();

      Func<TreeWalker<string, int>, int> countBelow = stance => Shape(stance.Subtree()).Count;

      var extended = door.Extend(countBelow);

      Assert.IsFalse(extended.HasFocus, "extend keeps the stance -- the unfocused stance included");
      Assert.AreEqual(5, countBelow(door), "the unfocused row: the observer applied directly, the whole-forest answer");

      var handleOfB = walkable.GetHandlesWithValues().Single(row => row.Node == "b").Handle;
      Assert.AreEqual(3, extended.At(handleOfB).GetValue(), "extract after extend recovers the observer at interiors");

      Assert.IsFalse(door.Duplicate().HasFocus, "duplicate commutes with the unfocused stance");
    }
  }
}
