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
      // the topology flows in, the unfocused walker comes out (the total door), no family
      // type participates.
      public TreeWalker<string, string> GetTreeWalker()
        => new TreeWalker<string, string>(this);

      public string GetNode(string handle) => handle;

      public Option<string> TryGetParent(string handle)
        => Parents.TryGetValue(handle, out var parent) ? new Option<string>(parent) : default;

      public Option<HandleAndSiblingIndex<string>> TryGetChildAt(string handle, int childIndex)
      {
        var children = Children[handle];

        return childIndex < children.Length
          ? new Option<HandleAndSiblingIndex<string>>(new HandleAndSiblingIndex<string>(children[childIndex], childIndex))
          : default;
      }

      public Option<HandleAndSiblingIndex<string>> TryGetRootAt(int rootIndex)
        => rootIndex == 0 ? new Option<HandleAndSiblingIndex<string>>(new HandleAndSiblingIndex<string>("a", 0)) : default;
    }

    [TestMethod]
    public void TheDoorMintsOverNativeAdjacency()
    {
      var door = new FamilyFreeTree().GetTreeWalker();

      Assert.IsFalse(door.HasFocus, "the door stands at the unfocused stance, above the roots");
      Assert.AreEqual("a", door.MoveToChild(0).Value.Focus, "the root is the unfocused stance's first child");
      Assert.AreEqual("a", door.MoveToChild(0).Value.GetNode());
    }

    [TestMethod]
    public void StepsWalkTheProviderTopology()
    {
      var walker = new FamilyFreeTree().GetTreeWalker().MoveToChild(0).Value;

      var firstChild = walker.MoveToChild(0);
      Assert.IsTrue(firstChild.HasValue);
      Assert.AreEqual("b", firstChild.Value.Focus);

      var grandchild = firstChild.Value.MoveToChild(0);
      Assert.IsTrue(grandchild.HasValue);
      Assert.AreEqual("d", grandchild.Value.Focus);

      var backUp = grandchild.Value.MoveToParent();
      Assert.IsTrue(backUp.HasValue);
      Assert.AreEqual("b", backUp.Value.Focus);

      var secondChild = walker.MoveToChild(1);
      Assert.IsTrue(secondChild.HasValue);
      Assert.AreEqual("c", secondChild.Value.Focus);

      Assert.IsFalse(walker.MoveToChild(2).HasValue);

      // The climb tops out standing on the unfocused stance -- an answer -- and stepping up from it
      // is the algebra's one upward miss.
      var top = walker.MoveToParent();
      Assert.IsTrue(top.HasValue, "a root's parent is the unfocused stance");
      Assert.IsFalse(top.Value.HasFocus);
      Assert.IsFalse(top.Value.MoveToParent().HasValue, "stepping up from the unfocused stance is the one upward miss");
    }

    [TestMethod]
    public void TheJumpReEntersOnStoredProviderHandles()
    {
      var walker = new FamilyFreeTree().GetTreeWalker();

      Assert.AreEqual("d", walker.At("d").GetNode());
      Assert.AreEqual("b", walker.At("d").MoveToParent().Value.Focus);
    }

    [TestMethod]
    public void BothSurfacesCoexistOnOneProvider()
    {
      var tree = new FamilyFreeTree();

      CollectionAssert.AreEqual(
        new[] { "a", "b", "d", "c" },
        tree.GetPreorderTraversal().ToArray());

      Assert.AreEqual("d", tree.GetTreeWalker().At("b").MoveToChild(0).Value.Focus);
    }
  }
}
