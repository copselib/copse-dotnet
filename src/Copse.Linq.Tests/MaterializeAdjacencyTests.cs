using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Stores;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The finite-izing escalation after the buffer re-parent (design-docs/WALKABLE_CONTRACT_DESIGN.md):
  // Materialize(BufferLayout.Preorder) IS the walker escalation -- every capture is walkable
  // ("captures are never address-poor"), the intersection the PoC once spelled as a separate
  // interface is ITreenumerableBuffer itself, and the erstwhile MaterializeWalkable alias is
  // gone (OPEN-3's collapse, completed). Deferred per the lazy-Materialize law; lens views and
  // native-adjacency providers still implement the walkable interface alone. The toy tree is
  // the UC-32 walkthrough's: a(b(d,e),c(f,g)), preorder ordinals a=0 b=1 d=2 e=3 c=4 f=5 g=6.
  [TestClass]
  public class MaterializeAdjacencyTests
  {
    private const string ToyTree = "a(b(d,e),c(f,g))";

    [TestMethod]
    public void Materialize_ToyTree_AdjacencyPinned()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

      Assert.AreEqual(BufferLayout.Preorder, walkable.NativeLayout, "the walker default: the ancestry-cheap capture");

      // Handles minted in preorder: a=0 b=1 d=2 e=3 c=4 f=5 g=6.
      Assert.AreEqual("a", WalkerLawProviders.TopologyOf(walkable).GetValue(0));
      Assert.AreEqual("d", WalkerLawProviders.TopologyOf(walkable).GetValue(2));
      Assert.AreEqual("c", WalkerLawProviders.TopologyOf(walkable).GetValue(4));

      Assert.IsFalse(WalkerLawProviders.TopologyOf(walkable).TryGetParent(0).HasValue);
      Assert.AreEqual(1, WalkerLawProviders.TopologyOf(walkable).TryGetParent(2).Value, "d's parent is b");
      Assert.AreEqual(4, WalkerLawProviders.TopologyOf(walkable).TryGetParent(6).Value, "g's parent is c");

      Assert.AreEqual(1, WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(0, 0).Value.Handle, "a's child 0 is b");
      Assert.AreEqual(4, WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(0, 1).Value.Handle, "a's child 1 is c");
      Assert.AreEqual(5, WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(4, 0).Value.Handle, "c's child 0 is f");
      Assert.IsFalse(WalkerLawProviders.TopologyOf(walkable).TryGetChildAt(0, 2).HasValue);

      Assert.AreEqual(0, WalkerLawProviders.TopologyOf(walkable).TryGetRootAt(0).Value.Handle);
      Assert.IsFalse(WalkerLawProviders.TopologyOf(walkable).TryGetRootAt(1).HasValue);
    }

    [TestMethod]
    public void Materialize_DefersTheCaptureToTheFirstPull()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree(ToyTree));

      var walkable = counting.Materialize(BufferLayout.Preorder);

      // Nothing opens at the call -- the lazy-Materialize law reaches the walker escalation.
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations);

      // The first adjacency call settles: one depth-first capture walk, handles minted.
      Assert.AreEqual(1, WalkerLawProviders.TopologyOf(walkable).TryGetParent(2).Value);
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);

      // Streaming both dimensions afterwards rides the capture; the source is retired.
      DrainVisits(walkable.GetDepthFirstTreenumerator());
      DrainVisits(walkable.GetBreadthFirstTreenumerator());
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
    }

    [TestMethod]
    public void Materialize_IsIdempotentOnACompliantCapture()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

      Assert.AreSame(walkable, walkable.Materialize(BufferLayout.Preorder), "a compliant capture is never re-captured");
    }

    [TestMethod]
    public void Materialize_VisitStream_MatchesTheSource_BothDimensions()
    {
      var walkable = TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder);

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
      // The lattice's walkable-only cell, post-re-parent: LENS VIEWS are its citizens -- a
      // Subtrees() label affords adjacency but wears no buffer marker (it is a severed VIEW
      // over someone else's store, owning nothing). A native-adjacency provider over an
      // infinite structure would sit in this cell too -- the type's silence about buffer-ness
      // is the infinity permission.
      var walkableOnly = WalkerLawProviders.TopologyOf(TreeSerializer.DeserializeDepthFirstTree(ToyTree)
        .Materialize(BufferLayout.Preorder)
        .Subtrees())
        .GetValue(0);

      Assert.IsFalse(walkableOnly is ITreenumerableBuffer<string>);
      Assert.IsTrue(
        TreeSerializer.DeserializeDepthFirstTree(ToyTree).Materialize(BufferLayout.Preorder) is ITreenumerableBuffer<string>,
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
