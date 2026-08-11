using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The restriction lens's pins: PruneAfter over a walkable stays walkable (statically -- the
  // overload resolves on the receiver's type), the adjacency half sheds children at matched
  // nodes, and the ORDER half is conformance-tied to its streaming oracle: the lens's visit
  // stream must equal the shipped operator's, by construction and by this test
  // (WALKER_USE_CASES.md's oracle-equivalence tradition).
  [TestClass]
  public class PruneAfterLensTests
  {
    // a(b(d,e),c(f,g)) -- preorder handles: a=0 b=1 d=2 e=3 c=4 f=5 g=6.
    private const string ToyTree = "a(b(d,e),c(f,g))";

    [TestMethod]
    public void PruneAfterLens_ShedsChildrenAtMatchedNodes_KeepsEverythingElse()
    {
      var lensed = TreeSerializer.DeserializeDepthFirstTree(ToyTree)
        .MaterializeWalkable()
        .PruneAfter(value => value == "b");

      // b survives with its ancestry and hands out no children; the rest is untouched.
      Assert.IsFalse(lensed.GetChildAt(1, 0).HasChild, "b sheds d and e");
      Assert.AreEqual(1, lensed.GetChildAt(0, 0).Child.Node, "a's child 0 is still b");
      Assert.AreEqual(4, lensed.GetChildAt(0, 1).Child.Node, "a's child 1 is still c");
      Assert.AreEqual(5, lensed.GetChildAt(4, 0).Child.Node, "c keeps f");
      Assert.AreEqual(0, lensed.GetParent(1).Parent, "ancestry untouched");
      Assert.AreEqual(0, lensed.GetRootAt(0).Child.Node, "roots always survive a prune-after");
    }

    [TestMethod]
    public void PruneAfterLens_OrderHalf_MatchesTheStreamingOracle_BothDimensions()
    {
      var lensed = TreeSerializer.DeserializeDepthFirstTree(ToyTree)
        .MaterializeWalkable()
        .PruneAfter(value => value == "b");

      var oracle = TreeSerializer.DeserializeDepthFirstTree(ToyTree).PruneAfter(value => value == "b");

      CollectionAssert.AreEqual(
        DrainVisits(oracle.GetDepthFirstTreenumerator()),
        DrainVisits(lensed.GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        DrainVisits(oracle.GetBreadthFirstTreenumerator()),
        DrainVisits(lensed.GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void PruneAfterLens_StacksWithoutALattice()
    {
      // Lenses compose by plain wrapping; the stream half underneath is prune-over-prune,
      // which the streaming tier's light tier merges in-tier as it always has -- the lattice
      // at work UNDER the lens, unaware walkables exist.
      var stacked = TreeSerializer.DeserializeDepthFirstTree(ToyTree)
        .MaterializeWalkable()
        .PruneAfter(value => value == "b")
        .PruneAfter(value => value == "c");

      Assert.IsFalse(stacked.GetChildAt(1, 0).HasChild, "b still sheds");
      Assert.IsFalse(stacked.GetChildAt(4, 0).HasChild, "c now sheds too");
      Assert.AreEqual(4, stacked.GetChildAt(0, 1).Child.Node, "both survive as leaves");

      var oracle = TreeSerializer.DeserializeDepthFirstTree(ToyTree)
        .PruneAfter(value => value == "b")
        .PruneAfter(value => value == "c");

      CollectionAssert.AreEqual(
        DrainVisits(oracle.GetDepthFirstTreenumerator()),
        DrainVisits(stacked.GetDepthFirstTreenumerator()));
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
  }
}
