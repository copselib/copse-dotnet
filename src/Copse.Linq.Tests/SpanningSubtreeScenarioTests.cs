using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // UC-32, the capstone, as an OPERATION: SpanningSubtree(targets) -- the minimum spanning
  // subtree of k nodes, returned as a walker standing at the spanning root over a fresh
  // O(kept) capture (design-docs/WALKER_USE_CASES.md §G). The suite pins the operation's whole
  // semantic surface -- the happy paths, the one honest miss (no targets), the unfocused-walker
  // answer for disjoint trees, and the k = 1 degenerate -- and keeps the DECOMPOSED arc as its own test: the
  // floor-by-floor walkthrough that proves the operation is a composition of shipped
  // pieces, not new machinery.
  [TestClass]
  public class SpanningSubtreeScenarioTests
  {
    [TestMethod]
    public void SpanningSubtree_ThreeTargets_TheWholeArcInOneCall()
    {
      // Source: a(b(d(h,i),e),c(f,g(j))). Targets h, i, g. The spanning subtree (nodes on
      // paths between targets) is a(b(d(h,i)),c(g)): e and f are off every path, and j sits
      // BELOW a leaf target -- between-ness excludes it.
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))")
        .Materialize();

      var interesting = new HashSet<string> { "h", "i", "g" };
      var spanning = walkable.SpanningSubtree(walkable.GetHandlesWithNodes().Where(row => interesting.Contains(row.Node)).Select(row => row.Handle));

      Assert.IsTrue(spanning.HasValue);
      Assert.AreEqual("a", spanning.Value.GetNode(), "the walker stands at the spanning root");

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i)),c(g))")),
        DrainScheduleOrder(spanning.Value.Subtree()),
        "the spanning subtree, exactly");
    }

    [TestMethod]
    public void SpanningSubtree_TargetsUnderOneBranch_TheRootIsMidTree()
    {
      // Targets h and e share the branch under b: the spanning subtree is b(d(h),e) --
      // the re-root lands mid-tree, depths compress, and nothing outside b survives.
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))")
        .Materialize(BufferLayout.Preorder);

      var interesting = new HashSet<string> { "h", "e" };
      var spanning = walkable.SpanningSubtree(walkable.GetHandlesWithNodes().Where(row => interesting.Contains(row.Node)).Select(row => row.Handle));

      Assert.IsTrue(spanning.HasValue);
      Assert.AreEqual("b", spanning.Value.GetNode(), "the spanning root is mid-tree");

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("b(d(h),e)")),
        DrainScheduleOrder(spanning.Value.Subtree()),
        "the spanning subtree of a mid-tree cluster");
    }

    // The operation's partiality surface: both misses are FACTS in the result type (never a
    // vocabulary-free LINQ exception, never a default walker), and k = 1 is not a miss.
    [TestMethod]
    public void SpanningSubtree_TheMisses_AndTheDegenerate()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize(BufferLayout.Preorder);

      // No targets: the spanning subtree of nothing is nothing.
      Assert.IsFalse(walkable.SpanningSubtree(Enumerable.Empty<int>()).HasValue, "k = 0 is an honest miss");

      // One target: the node alone -- the fold is a no-op and the clamp keeps exactly it.
      var single = walkable.SpanningSubtree(walkable.GetHandlesWithNodes().Where(row => row.Node == "d").Select(row => row.Handle));
      Assert.IsTrue(single.HasValue);
      Assert.AreEqual("d", single.Value.GetNode());
      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("d")),
        DrainScheduleOrder(single.Value.Subtree()),
        "the spanning subtree of one node is the node");

      // Disjoint trees in a forest: the common ancestor is the UNFOCUSED STANCE, and the answer
      // is the spanning forest under an unfocused walker -- one spanning subtree per touched tree, the
      // off-path subtree (e) clamped away like any other.
      var forest = TreeSerializer.DeserializeDepthFirstTree("a(b,e),c(d)").Materialize(BufferLayout.Preorder);
      var disjoint = forest.SpanningSubtree(forest.GetHandlesWithNodes().Where(row => row.Node == "b" || row.Node == "d").Select(row => row.Handle));

      Assert.IsTrue(disjoint.HasValue, "disjoint targets answer");
      Assert.IsFalse(disjoint.Value.HasFocus, "the spanning of disjoint targets stands at the unfocused stance");
      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("a(b),c(d)")),
        DrainScheduleOrder(disjoint.Value.Subtree()),
        "the spanning forest under the unfocused walker, hoisted");
    }

    // The per-capture clause, made visible at the operation's seam: the returned walker
    // stands on a NEW capture, so its handles are that capture's ordinals -- the spanning
    // root is handle 0 THERE, whatever it was in the source.
    [TestMethod]
    public void SpanningSubtree_TheResultIsANewCapture_WithItsOwnHandleSpace()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize(BufferLayout.Preorder);

      var targets = walkable.GetHandlesWithNodes().Where(row => row.Node == "d" || row.Node == "e").Select(row => row.Handle).ToList();
      Assert.IsTrue(targets.All(handle => handle >= 2), "the targets sit deep in the SOURCE handle space");

      var spanning = walkable.SpanningSubtree(targets);

      Assert.AreEqual(0, spanning.Value.Focus, "the spanning root is ordinal zero of ITS OWN capture");
      Assert.AreEqual("b", spanning.Value.GetNode());
    }

    // The DECOMPOSED arc -- the floor-by-floor walkthrough the operation distills, kept
    // running so the composition claim stays executable: streaming algebra, organic
    // Materialize, rowid-idiom acquisition (the search law: consumer LINQ over GetHandlesWithNodes), walker-first LCA fold, the severed re-root,
    // and the handle-decorated stream clamp (the climbs recorded ARE the membership memo).
    [TestMethod]
    public void TheCapstoneDecomposed_EveryFloorInOneArc()
    {
      var relevant = TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))")
        .Where(context => true);

      var walkable = relevant.Materialize();

      var interesting = new HashSet<string> { "h", "i", "g" };
      var targets = walkable.GetHandlesWithNodes().Where(row => interesting.Contains(row.Node)).Select(row => row.Handle).ToList();
      Assert.AreEqual(3, targets.Count, "acquisition found the targets");

      var lca = targets
        .Select(handle => walkable.GetTreeWalkerAt(handle))
        .Aggregate((left, right) => LowestCommonAncestor(left, right));

      Assert.AreEqual("a", lca.GetNode(), "the three targets' LCA");

      var keptHandles = new HashSet<int> { lca.Focus };
      foreach (var target in targets)
        foreach (var pathHandle in PathToAncestor(walkable.GetTreeWalkerAt(target), lca.Focus))
          keptHandles.Add(pathHandle);

      var spanning = lca.Subtree();

      var clamped = spanning
        .Extend((topology, handle) => new HandleAndNode<int, string>(handle, topology.GetNode(handle)))
        .PruneBefore(pair => !keptHandles.Contains(pair.Handle))
        .Select(pair => pair.Node);

      CollectionAssert.AreEqual(
        DrainScheduleOrder(TreeSerializer.DeserializeDepthFirstTree("a(b(d(h,i)),c(g))")),
        DrainScheduleOrder(clamped),
        "the decomposition and the operation agree");
    }

    // ---------------------------------------------------------------- the hand-rolled axes
    // Retained for the decomposed walkthrough; the OPERATION carries its own copies, and
    // the axis wave promotes them to public extensions. Walker-first and TOTAL: climbs top
    // out on the unfocused stance, so disjoint trees meet there -- an answer, not a miss.

    private static TreeWalker<string, int> LowestCommonAncestor(TreeWalker<string, int> first, TreeWalker<string, int> second)
    {
      if (!first.HasFocus)
        return first;

      var firstPath = new HashSet<int>();
      var stance = first;

      while (stance.HasFocus)
      {
        firstPath.Add(stance.Focus);
        stance = stance.MoveToParent().Value;
      }

      var candidate = second;

      while (candidate.HasFocus && !firstPath.Contains(candidate.Focus))
        candidate = candidate.MoveToParent().Value;

      return candidate;
    }

    private static IEnumerable<int> PathToAncestor(TreeWalker<string, int> descendant, int ancestorFocus)
    {
      var stance = descendant;

      while (stance.Focus != ancestorFocus)
      {
        yield return stance.Focus;

        var up = stance.MoveToParent().Value;
        if (!up.HasFocus)
          throw new System.InvalidOperationException(
            "the given focus is not an ancestor of the starting stance -- a poisoned handle would have climbed to the unfocused stance here");

        stance = up;
      }
    }

    private static List<string> DrainScheduleOrder(ITreenumerable<string> tree)
    {
      var schedule = new List<string>();

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            schedule.Add(treenumerator.Node);
      }

      return schedule;
    }
  }
}
