using Copse.Core;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Tests
{
  // PoC pins for the walkable contract (docs/WALKER_DESIGN.md) over the flat family's preorder
  // store: the parent axis and the INDEXED child axis (the VisualTreeHelper shape -- every
  // adjacency member returns by value, nothing allocates) against hand-computed adjacency, roots
  // as the virtual forest-root's indexed child group, and the conformance pin that the
  // walkable's visit stream IS PreorderTreenumerable's (WALKER_USE_CASES.md UC-26: the
  // everything-walk and the native stream must agree).
  [TestClass]
  public class WalkableTreenumerableTests
  {
    //     a
    //    / \
    //   b   e
    //  / \
    // c   d
    private static WalkablePreorderTreenumerable<string, PreorderArrayStore<string>> SingleTree()
      => new(new PreorderArrayStore<string>(
        ["a", "b", "c", "d", "e"],
        [5, 3, 1, 1, 1]));

    // Forest: a(b), c
    private static WalkablePreorderTreenumerable<string, PreorderArrayStore<string>> Forest()
      => new(new PreorderArrayStore<string>(
        ["a", "b", "c"],
        [2, 1, 1]));

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

      var parentOfB = walkable.GetParent(1);
      Assert.IsTrue(parentOfB.HasParent);
      Assert.AreEqual(0, parentOfB.Parent);

      Assert.AreEqual(1, walkable.GetParent(2).Parent, "c's parent is b");
      Assert.AreEqual(1, walkable.GetParent(3).Parent, "d's parent is b");
      Assert.AreEqual(0, walkable.GetParent(4).Parent, "e's parent is a");
    }

    [TestMethod]
    public void GetChildAt_YieldsChildrenInSiblingOrder()
    {
      var walkable = SingleTree();

      CollectionAssert.AreEqual(
        new List<(int, int)> { (1, 0), (4, 1) },
        Children(walkable, 0),
        "a's children are b and e");

      CollectionAssert.AreEqual(
        new List<(int, int)> { (2, 0), (3, 1) },
        Children(walkable, 1),
        "b's children are c and d");

      Assert.AreEqual(0, Children(walkable, 2).Count, "c is a leaf");
    }

    [TestMethod]
    public void GetChildAt_ProbesPastTheEndAndBelowZeroMiss()
    {
      var walkable = SingleTree();

      Assert.IsFalse(walkable.GetChildAt(0, 2).HasChild, "a has two children");
      Assert.IsFalse(walkable.GetChildAt(0, -1).HasChild);
      Assert.IsFalse(walkable.GetChildAt(2, 0).HasChild, "a leaf has none");
    }

    [TestMethod]
    public void GetChildCount_PinnedOnKnownTree()
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
        new List<(int, int)> { (0, 0), (2, 1) },
        Roots(forest),
        "both roots, in sibling order, sized by their subtree spans");

      Assert.IsFalse(forest.GetParent(2).HasParent, "the second root has no parent");
      Assert.AreEqual(0, forest.GetParent(1).Parent);
    }

    [TestMethod]
    public void ParentChain_WalksToTheRoot()
    {
      var walkable = SingleTree();

      var ancestorValues = new List<string>();
      var parentResult = walkable.GetParent(3);

      while (parentResult.HasParent)
      {
        ancestorValues.Add(walkable.GetValue(parentResult.Parent));

        parentResult = walkable.GetParent(parentResult.Parent);
      }

      CollectionAssert.AreEqual(new List<string> { "b", "a" }, ancestorValues, "d's ancestors, nearest first");
    }

    [TestMethod]
    public void VisitStream_MatchesPreorderTreenumerable_BothDimensions()
    {
      var values = new[] { "a", "b", "c", "d", "e" };
      var subtreeSizes = new[] { 5, 3, 1, 1, 1 };

      var walkable = new WalkablePreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(values, subtreeSizes));
      var native = new PreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(values, subtreeSizes));

      CollectionAssert.AreEqual(
        DrainVisits(native.GetDepthFirstTreenumerator()),
        DrainVisits(walkable.GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        DrainVisits(native.GetBreadthFirstTreenumerator()),
        DrainVisits(walkable.GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void AllAdjacencyAxes_ShareTheOneLazyIndex()
    {
      var walkable = SingleTree();

      // Child probes first, then upward steps, then counts -- every axis rides the one build.
      var childrenOfA = Children(walkable, 0);

      Assert.AreEqual(2, childrenOfA.Count);
      Assert.AreEqual(0, walkable.GetParent(childrenOfA[1].Node).Parent, "e's parent is a (index 0)");
      Assert.AreEqual(0, walkable.GetParent(1).Parent);
      Assert.AreEqual(2, walkable.GetChildCount(1));
    }
  }
}
