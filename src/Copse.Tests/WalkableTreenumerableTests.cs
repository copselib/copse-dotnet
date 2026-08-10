using Copse.Core;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Tests
{
  // PoC pins for the walkable contract (docs/WALKER_DESIGN.md) over the flat family's preorder
  // store: the parent/child axes against hand-computed adjacency, roots as the virtual
  // forest-root's children, and the conformance pin that the walkable's visit stream IS
  // PreorderTreenumerable's (WALKER_USE_CASES.md UC-26: the everything-walk and the native
  // stream must agree).
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

    private static List<(int Node, int SiblingIndex)> Drain(
      PreorderStoreChildEnumerator<string, PreorderArrayStore<string>> childEnumerator)
    {
      var children = new List<(int, int)>();

      using (childEnumerator)
      {
        var childResult = childEnumerator.MoveNext();

        while (childResult.HasChild)
        {
          children.Add((childResult.Child.Node, childResult.Child.SiblingIndex));

          childResult = childEnumerator.MoveNext();
        }
      }

      return children;
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
    public void GetChildEnumerator_YieldsChildSpansInSiblingOrder()
    {
      var walkable = SingleTree();

      CollectionAssert.AreEqual(
        new List<(int, int)> { (1, 0), (4, 1) },
        Drain(walkable.GetChildEnumerator(0)),
        "a's children are b and e");

      CollectionAssert.AreEqual(
        new List<(int, int)> { (2, 0), (3, 1) },
        Drain(walkable.GetChildEnumerator(1)),
        "b's children are c and d");

      Assert.AreEqual(0, Drain(walkable.GetChildEnumerator(2)).Count, "c is a leaf");
    }

    [TestMethod]
    public void GetRootEnumerator_YieldsTheVirtualForestRootsChildren()
    {
      CollectionAssert.AreEqual(
        new List<(int, int)> { (0, 0) },
        Drain(SingleTree().GetRootEnumerator()));

      var forest = Forest();

      CollectionAssert.AreEqual(
        new List<(int, int)> { (0, 0), (2, 1) },
        Drain(forest.GetRootEnumerator()),
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
    public void GetParent_AfterChildNavigation_TheLazyIndexIsUnperturbed()
    {
      var walkable = SingleTree();

      // Child pulls first (no parent index yet), then upward steps force the lazy build.
      var childrenOfA = Drain(walkable.GetChildEnumerator(0));

      Assert.AreEqual(2, childrenOfA.Count);
      Assert.AreEqual(0, walkable.GetParent(childrenOfA[1].Node).Parent, "e's parent is a (index 0)");
      Assert.AreEqual(0, walkable.GetParent(1).Parent);
    }
  }
}
