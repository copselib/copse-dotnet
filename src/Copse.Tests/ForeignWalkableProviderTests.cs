using Copse.Core;
using Copse.Linq;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Tests
{
  // The provider-mint pin (2026-08-15): a walkable implemented ENTIRELY OUTSIDE the family,
  // over its own native adjacency, minting walkers through the PUBLIC TreeWalker constructor.
  // This assembly holds no InternalsVisibleTo grant from Copse.Core, so this file COMPILING
  // is itself the proof that the contract is implementable by third parties -- the door-only
  // charter's two-audience story (SPI for providers, walker for consumers) closed by the
  // type system instead of promised by documentation. Handles are strings -- the provider's
  // own node identities, never ordinals of a capture -- pinning the THandle genericity a
  // Materialize-delegating door would forfeit.
  [TestClass]
  public class ForeignWalkableProviderTests
  {
    // a(b(d), c) -- the provider's native structure: two dictionaries, no family machinery.
    private sealed class FamilyFreeTree : IWalkableTreenumerable<string, string>, ITreeTopology<string, string>
    {
      private static readonly Dictionary<string, string[]> Children = new Dictionary<string, string[]>
      {
        ["a"] = new[] { "b", "c" },
        ["b"] = new[] { "d" },
        ["c"] = new string[0],
        ["d"] = new string[0],
      };

      private static readonly Dictionary<string, string> Parents = new Dictionary<string, string>
      {
        ["b"] = "a",
        ["c"] = "a",
        ["d"] = "b",
      };

      // The streaming half, for free: the provider IS a topology, and Tree.FromTopology
      // walks any topology with the engine (2026-08-15 -- the second half of the
      // open-ecosystem story; before it, this test hand-rolled a child enumerator and an
      // engine tree to stream, twenty lines of boilerplate the factory replaces).
      private readonly ITreenumerable<string> _Streaming;

      public FamilyFreeTree()
      {
        _Streaming = Tree.FromTopology(this);
      }

      public ITreenumerator<string> GetDepthFirstTreenumerator() => _Streaming.GetDepthFirstTreenumerator();

      public ITreenumerator<string> GetBreadthFirstTreenumerator() => _Streaming.GetBreadthFirstTreenumerator();

      // The door: the provider mint in its natural habitat. Construction is the trust door --
      // the topology flows in, the walker comes out, no family type participates.
      public TreeWalkerResult<string, string> TryGetTreeWalker()
        => new TreeWalkerResult<string, string>(new TreeWalker<string, string>(this, "a"));

      public string GetValue(string handle) => handle;

      public ParentResult<string> TryGetParent(string handle)
        => Parents.TryGetValue(handle, out var parent) ? new ParentResult<string>(parent) : default;

      public ChildResult<string> TryGetChildAt(string handle, int childIndex)
      {
        var children = Children[handle];

        return childIndex < children.Length
          ? new ChildResult<string>(new NodeAndSiblingIndex<string>(children[childIndex], childIndex))
          : default;
      }

      public ChildResult<string> TryGetRootAt(int rootIndex)
        => rootIndex == 0 ? new ChildResult<string>(new NodeAndSiblingIndex<string>("a", 0)) : default;
    }

    [TestMethod]
    public void TheDoorMintsOverNativeAdjacency()
    {
      var door = new FamilyFreeTree().TryGetTreeWalker();

      Assert.IsTrue(door.HasWalker);
      Assert.AreEqual("a", door.Walker.Focus);
      Assert.AreEqual("a", door.Walker.GetValue());
    }

    [TestMethod]
    public void StepsWalkTheProviderTopology()
    {
      var walker = new FamilyFreeTree().TryGetTreeWalker().Walker;

      var firstChild = walker.MoveToChild(0);
      Assert.IsTrue(firstChild.HasWalker);
      Assert.AreEqual("b", firstChild.Walker.Focus);

      var grandchild = firstChild.Walker.MoveToChild(0);
      Assert.IsTrue(grandchild.HasWalker);
      Assert.AreEqual("d", grandchild.Walker.Focus);

      var backUp = grandchild.Walker.MoveToParent();
      Assert.IsTrue(backUp.HasWalker);
      Assert.AreEqual("b", backUp.Walker.Focus);

      var secondChild = walker.MoveToChild(1);
      Assert.IsTrue(secondChild.HasWalker);
      Assert.AreEqual("c", secondChild.Walker.Focus);

      Assert.IsFalse(walker.MoveToChild(2).HasWalker);
      Assert.IsFalse(walker.MoveToParent().HasWalker);
    }

    [TestMethod]
    public void TheJumpReEntersOnStoredProviderHandles()
    {
      var walker = new FamilyFreeTree().TryGetTreeWalker().Walker;

      Assert.AreEqual("d", walker.At("d").GetValue());
      Assert.AreEqual("b", walker.At("d").MoveToParent().Walker.Focus);
    }

    [TestMethod]
    public void BothSurfacesCoexistOnOneProvider()
    {
      var tree = new FamilyFreeTree();

      CollectionAssert.AreEqual(
        new[] { "a", "b", "d", "c" },
        tree.GetPreorderTraversal().ToArray());

      Assert.AreEqual("d", tree.TryGetTreeWalker().Walker.At("b").MoveToChild(0).Walker.Focus);
    }
  }
}
