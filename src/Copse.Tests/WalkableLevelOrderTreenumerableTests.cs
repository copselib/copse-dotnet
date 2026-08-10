using Copse.Core;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Tests
{
  // The level-order walkable's pins: WalkableTreenumerableTests' mirror over the dual encoding,
  // plus the cross-family pin -- the two walkable stores hold the SAME logical tree, so their
  // visit streams must agree with each other, not just with their own native treenumerables.
  // The indexed child contract needs NO index build here: the encoding is already the
  // VisualTreeHelper shape (contiguous child runs behind a first-child offset).
  [TestClass]
  public class WalkableLevelOrderTreenumerableTests
  {
    //     a
    //    / \
    //   b   e
    //  / \
    // c   d
    //
    // Level order: a(0), b(1), e(2), c(3), d(4).
    private static WalkableLevelOrderTreenumerable<string, LevelOrderArrayStore<string>> SingleTree()
      => new(new LevelOrderArrayStore<string>(
        ["a", "b", "e", "c", "d"],
        firstChildIndices: [1, 3, 0, 0, 0],
        childCounts: [2, 2, 0, 0, 0],
        rootCount: 1));

    // Forest: a(b), c -- level order: a(0), c(1), b(2).
    private static WalkableLevelOrderTreenumerable<string, LevelOrderArrayStore<string>> Forest()
      => new(new LevelOrderArrayStore<string>(
        ["a", "c", "b"],
        firstChildIndices: [2, 0, 0],
        childCounts: [1, 0, 0],
        rootCount: 2));

    private static List<(int Node, int SiblingIndex)> Children(
      IWalkableTreenumerable<string, int> walkable,
      int node)
    {
      var children = new List<(int, int)>();

      for (var childIndex = 0; ; childIndex++)
      {
        var childResult = walkable.GetChildAt(node, childIndex);

        if (!childResult.HasChild)
          return children;

        children.Add((childResult.Child.Node, childResult.Child.SiblingIndex));
      }
    }

    private static List<(int Node, int SiblingIndex)> Roots(IWalkableTreenumerable<string, int> walkable)
    {
      var roots = new List<(int, int)>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = walkable.GetRootAt(rootIndex);

        if (!rootResult.HasChild)
          return roots;

        roots.Add((rootResult.Child.Node, rootResult.Child.SiblingIndex));
      }
    }

    private static List<(TreenumeratorMode Mode, string Node, int VisitCount, NodePosition Position)> DrainVisits(
      ITreenumerator<string> treenumerator)
    {
      var visits = new List<(TreenumeratorMode, string, int, NodePosition)>();

      using (treenumerator)
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add((treenumerator.Mode, treenumerator.Node, treenumerator.VisitCount, treenumerator.Position));
      }

      return visits;
    }

    [TestMethod]
    public void GetParent_PinnedOnKnownTree()
    {
      var walkable = SingleTree();

      Assert.IsFalse(walkable.GetParent(0).HasParent, "a is a root");
      Assert.AreEqual(0, walkable.GetParent(1).Parent, "b's parent is a");
      Assert.AreEqual(0, walkable.GetParent(2).Parent, "e's parent is a");
      Assert.AreEqual(1, walkable.GetParent(3).Parent, "c's parent is b");
      Assert.AreEqual(1, walkable.GetParent(4).Parent, "d's parent is b");
    }

    [TestMethod]
    public void GetChildAt_YieldsContiguousRunsInSiblingOrder()
    {
      var walkable = SingleTree();

      CollectionAssert.AreEqual(
        new List<(int, int)> { (1, 0), (2, 1) },
        Children(walkable, 0),
        "a's children are b and e -- one contiguous run");

      CollectionAssert.AreEqual(
        new List<(int, int)> { (3, 0), (4, 1) },
        Children(walkable, 1),
        "b's children are c and d");

      Assert.AreEqual(0, Children(walkable, 3).Count, "c is a leaf");
      Assert.IsFalse(walkable.GetChildAt(0, 2).HasChild, "past the run's end");
      Assert.IsFalse(walkable.GetChildAt(0, -1).HasChild);
    }

    [TestMethod]
    public void GetChildCount_ProbesTheRun()
    {
      var walkable = SingleTree();

      Assert.AreEqual(2, walkable.GetChildCount(0));
      Assert.AreEqual(2, walkable.GetChildCount(1));
      Assert.AreEqual(0, walkable.GetChildCount(2));
      Assert.AreEqual(0, walkable.GetChildCount(4));
    }

    [TestMethod]
    public void GetRootAt_YieldsTheVirtualForestRootsChildren()
    {
      CollectionAssert.AreEqual(
        new List<(int, int)> { (0, 0) },
        Roots(SingleTree()));

      var forest = Forest();

      CollectionAssert.AreEqual(
        new List<(int, int)> { (0, 0), (1, 1) },
        Roots(forest),
        "roots are the leading entries, root ordinal == buffer index");

      Assert.IsFalse(forest.GetParent(1).HasParent, "the second root has no parent");
      Assert.AreEqual(0, forest.GetParent(2).Parent);
    }

    [TestMethod]
    public void ParentChain_WalksToTheRoot()
    {
      var walkable = SingleTree();

      var ancestorValues = new List<string>();
      var parentResult = walkable.GetParent(4);

      while (parentResult.HasParent)
      {
        ancestorValues.Add(walkable.GetValue(parentResult.Parent));

        parentResult = walkable.GetParent(parentResult.Parent);
      }

      CollectionAssert.AreEqual(new List<string> { "b", "a" }, ancestorValues, "d's ancestors, nearest first");
    }

    [TestMethod]
    public void VisitStream_MatchesLevelOrderTreenumerable_BothDimensions()
    {
      var native = new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(
        new LevelOrderArrayStore<string>(
          ["a", "b", "e", "c", "d"],
          firstChildIndices: [1, 3, 0, 0, 0],
          childCounts: [2, 2, 0, 0, 0],
          rootCount: 1));

      CollectionAssert.AreEqual(
        DrainVisits(native.GetDepthFirstTreenumerator()),
        DrainVisits(SingleTree().GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        DrainVisits(native.GetBreadthFirstTreenumerator()),
        DrainVisits(SingleTree().GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void VisitStream_AgreesWithThePreorderWalkable_SameLogicalTree()
    {
      // The cross-family pin: the same tree behind both flat encodings must present identically
      // through both walkable citizens -- the duals differ in axis costs, never in the tree.
      var preorderWalkable = new WalkablePreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(
          ["a", "b", "c", "d", "e"],
          [5, 3, 1, 1, 1]));

      CollectionAssert.AreEqual(
        DrainVisits(preorderWalkable.GetDepthFirstTreenumerator()),
        DrainVisits(SingleTree().GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        DrainVisits(preorderWalkable.GetBreadthFirstTreenumerator()),
        DrainVisits(SingleTree().GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void ParentAxis_AgreesWithThePreorderWalkable_ValueForValue()
    {
      // Same logical tree, different ordinals: the parent RELATION must agree once handles are
      // resolved to values. (Handles are encoding-local; values are the shared truth.)
      var preorderWalkable = new WalkablePreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(
          ["a", "b", "c", "d", "e"],
          [5, 3, 1, 1, 1]));
      var levelOrderWalkable = SingleTree();

      var preorderParents = new Dictionary<string, string>();
      for (var nodeIndex = 0; nodeIndex < 5; nodeIndex++)
      {
        var parentResult = preorderWalkable.GetParent(nodeIndex);
        preorderParents[preorderWalkable.GetValue(nodeIndex)] =
          parentResult.HasParent ? preorderWalkable.GetValue(parentResult.Parent) : null;
      }

      var levelOrderParents = new Dictionary<string, string>();
      for (var nodeIndex = 0; nodeIndex < 5; nodeIndex++)
      {
        var parentResult = levelOrderWalkable.GetParent(nodeIndex);
        levelOrderParents[levelOrderWalkable.GetValue(nodeIndex)] =
          parentResult.HasParent ? levelOrderWalkable.GetValue(parentResult.Parent) : null;
      }

      CollectionAssert.AreEquivalent(preorderParents, levelOrderParents);
    }

    [TestMethod]
    public void ChildAxis_AgreesWithThePreorderWalkable_ValueForValue()
    {
      // The indexed child axis, cross-family: child k of the same VALUE must be the same value
      // through both encodings, whatever the ordinals underneath.
      var preorderWalkable = new WalkablePreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(
          ["a", "b", "c", "d", "e"],
          [5, 3, 1, 1, 1]));
      var levelOrderWalkable = SingleTree();

      // a's child 1: e through both. b's child 0: c through both.
      Assert.AreEqual("e", preorderWalkable.GetValue(preorderWalkable.GetChildAt(0, 1).Child.Node));
      Assert.AreEqual("e", levelOrderWalkable.GetValue(levelOrderWalkable.GetChildAt(0, 1).Child.Node));
      Assert.AreEqual("c", preorderWalkable.GetValue(preorderWalkable.GetChildAt(1, 0).Child.Node));
      Assert.AreEqual("c", levelOrderWalkable.GetValue(levelOrderWalkable.GetChildAt(1, 0).Child.Node));
    }
  }
}
