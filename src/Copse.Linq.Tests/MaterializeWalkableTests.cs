using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The finite-izing escalation after the buffer re-parent (docs/WALKABLE_CONTRACT_DESIGN.md):
  // MaterializeWalkable is a declared-preorder Materialize, and the intersection the PoC once
  // spelled as a separate interface is now ITreenumerableBuffer itself -- every capture is
  // walkable ("captures are never address-poor"), deferred per the lazy-Materialize law;
  // native-adjacency walkables still implement the walkable interface alone. The toy tree is
  // the UC-32 walkthrough's: a(b(d,e),c(f,g)), preorder ordinals a=0 b=1 d=2 e=3 c=4 f=5 g=6.
  [TestClass]
  public class MaterializeWalkableTests
  {
    private const string ToyTree = "a(b(d,e),c(f,g))";

    [TestMethod]
    public void MaterializeWalkable_ToyTree_AdjacencyPinned()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      Assert.AreEqual(BufferLayout.Preorder, walkable.NativeLayout, "the walker default: the ancestry-cheap capture");

      // Handles minted in preorder: a=0 b=1 d=2 e=3 c=4 f=5 g=6.
      Assert.AreEqual("a", walkable.GetValue(0));
      Assert.AreEqual("d", walkable.GetValue(2));
      Assert.AreEqual("c", walkable.GetValue(4));

      Assert.IsFalse(walkable.GetParent(0).HasParent);
      Assert.AreEqual(1, walkable.GetParent(2).Parent, "d's parent is b");
      Assert.AreEqual(4, walkable.GetParent(6).Parent, "g's parent is c");

      Assert.AreEqual(1, walkable.GetChildAt(0, 0).Child.Node, "a's child 0 is b");
      Assert.AreEqual(4, walkable.GetChildAt(0, 1).Child.Node, "a's child 1 is c");
      Assert.AreEqual(5, walkable.GetChildAt(4, 0).Child.Node, "c's child 0 is f");
      Assert.IsFalse(walkable.GetChildAt(0, 2).HasChild);

      Assert.AreEqual(0, walkable.GetRootAt(0).Child.Node);
      Assert.IsFalse(walkable.GetRootAt(1).HasChild);
    }

    [TestMethod]
    public void MaterializeWalkable_DefersTheCaptureToTheFirstPull()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree(ToyTree));

      var walkable = counting.MaterializeWalkable();

      // Nothing opens at the call -- the lazy-Materialize law reaches the walker escalation.
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations);

      // The first adjacency call settles: one depth-first capture walk, handles minted.
      Assert.AreEqual(1, walkable.GetParent(2).Parent);
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);

      // Streaming both dimensions afterwards rides the capture; the source is retired.
      DrainVisits(walkable.GetDepthFirstTreenumerator());
      DrainVisits(walkable.GetBreadthFirstTreenumerator());
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
    }

    [TestMethod]
    public void MaterializeWalkable_IsIdempotentOnTheIntersection()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      Assert.AreSame(walkable, walkable.MaterializeWalkable(), "an intersection citizen is never re-captured");
    }

    [TestMethod]
    public void MaterializeWalkable_VisitStream_MatchesTheSource_BothDimensions()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable();

      CollectionAssert.AreEqual(
        DrainVisits(TreeSerializer.DeserializeDepthFirstTree(ToyTree).GetDepthFirstTreenumerator()),
        DrainVisits(walkable.GetDepthFirstTreenumerator()));

      CollectionAssert.AreEqual(
        DrainVisits(TreeSerializer.DeserializeDepthFirstTree(ToyTree).GetBreadthFirstTreenumerator()),
        DrainVisits(walkable.GetBreadthFirstTreenumerator()));
    }

    [TestMethod]
    public void TheFinitenessLaw_WalkableAloneMakesNoCaptureClaim()
    {
      // The lattice's walkable-only cell: a store walkable affords adjacency but wears no
      // buffer marker (the interfaces are orthogonal; the intersection is a separate, deliberate
      // citizen). A native-adjacency provider over an infinite structure would sit in this cell
      // too -- the type's silence about buffer-ness is the infinity permission.
      var walkableOnly = new WalkablePreorderTreenumerable<string, PreorderArrayStore<string>>(
        new PreorderArrayStore<string>(["a", "b", "c"], [3, 1, 1]));

      Assert.IsFalse((object)walkableOnly is ITreenumerableBuffer<string>);
      Assert.IsTrue(
        TreeSerializer.DeserializeDepthFirstTree(ToyTree).MaterializeWalkable() is ITreenumerableBuffer<string>,
        "the escalation's result wears both");
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

    private sealed class CountingSource : ITreenumerable<string>
    {
      public CountingSource(ITreenumerable<string> inner) => _Inner = inner;

      private readonly ITreenumerable<string> _Inner;

      public int DepthFirstEnumerations { get; private set; }
      public int BreadthFirstEnumerations { get; private set; }

      public ITreenumerator<string> GetDepthFirstTreenumerator()
      {
        DepthFirstEnumerations++;
        return _Inner.GetDepthFirstTreenumerator();
      }

      public ITreenumerator<string> GetBreadthFirstTreenumerator()
      {
        BreadthFirstEnumerations++;
        return _Inner.GetBreadthFirstTreenumerator();
      }
    }
  }
}
