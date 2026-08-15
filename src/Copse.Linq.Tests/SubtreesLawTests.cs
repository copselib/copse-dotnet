using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The cofree duplicate's laws, pinned nodewise against Subtrees() -- the comonad's
  // duplicate in the presentation where every label is the subtree rooted at its node
  // (docs/CATEGORY_THEORY_SURVEY.md §4; the Store presentation's laws live in
  // WalkerComonadLawTests). The counits: the subtree at a root is that root's whole tree
  // (extract after duplicate), and every label's root value is the original value (map
  // extract after duplicate). Co-associativity: a subtree of a subtree is the deeper
  // subtree. Severance is part of the presentation and pinned as such: a label's root has
  // no parent, and severance happens AT the root and nowhere else.
  [TestClass]
  public class SubtreesLawTests
  {
    // Each tree with its top-level split: the expected whole-tree label at each root.
    private static readonly (string Tree, string[] RootTrees)[] Forests =
    {
      ("a", new[] { "a" }),
      ("a(b(c))", new[] { "a(b(c))" }),
      ("a(b,c)", new[] { "a(b,c)" }),
      ("a,b,c", new[] { "a", "b", "c" }),
      ("a(b(d,e),c(f,g))", new[] { "a(b(d,e),c(f,g))" }),
      ("a,b(d),c(e(f))", new[] { "a", "b(d)", "c(e(f))" }),
    };

    // The hand-pinned interior expectations speak preorder ordinals, so W stays the preorder
    // provider; every law-shaped test rides the full provider fan-out below.
    private static IWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree).Materialize(BufferLayout.Preorder);

    private static IEnumerable<(string Tree, string[] RootTrees, IWalkableTreenumerable<string, int> Walkable)> AllWalkables()
      => Forests.SelectMany(forest => WalkerLawProviders.Walkables(forest.Tree).Select(walkable => (forest.Tree, forest.RootTrees, walkable)));

    [TestMethod]
    public void Counit_TheSubtreeAtARoot_IsThatRootsWholeTree()
    {
      foreach (var (tree, rootTrees, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        for (var rootIndex = 0; rootIndex < rootTrees.Length; rootIndex++)
        {
          var rootHandle = WalkerLawProviders.TopologyOf(walkable).TryGetRootAt(rootIndex).Child.Node;

          AssertEquivalent(
            TreeSerializer.DeserializeDepthFirstTree(rootTrees[rootIndex]),
            WalkerLawProviders.TopologyOf(subtrees).GetValue(rootHandle),
            $"extract∘duplicate at root {rootIndex} [{tree}]");
        }
      }
    }

    [TestMethod]
    public void Counit_EveryLabelsRootValue_IsTheOriginalValue()
    {
      foreach (var (tree, _, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        foreach (var handle in walkable.GetHandles())
        {
          var label = WalkerLawProviders.TopologyOf(subtrees).GetValue(handle);

          Assert.AreEqual(WalkerLawProviders.TopologyOf(walkable).GetValue(handle), WalkerLawProviders.TopologyOf(label).GetValue(handle), $"map(extract)∘duplicate [{tree}]");

          var labelRoot = WalkerLawProviders.TopologyOf(label).TryGetRootAt(0);
          Assert.IsTrue(labelRoot.HasChild, $"label has a root [{tree}]");
          Assert.AreEqual(handle, labelRoot.Child.Node, $"the label's root is its node [{tree}]");
          Assert.AreEqual(0, labelRoot.Child.SiblingIndex, $"the label's root re-roots to sibling 0 [{tree}]");
          Assert.IsFalse(WalkerLawProviders.TopologyOf(label).TryGetRootAt(1).HasChild, $"a subtree is single-rooted [{tree}]");
        }
      }
    }

    [TestMethod]
    public void Severance_IsExactlyAtTheRoot()
    {
      foreach (var (tree, _, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        foreach (var handle in walkable.GetHandles())
        {
          var label = WalkerLawProviders.TopologyOf(subtrees).GetValue(handle);

          Assert.IsFalse(WalkerLawProviders.TopologyOf(label).TryGetParent(handle).HasParent, $"the label's root is parentless [{tree}]");

          foreach (var descendant in Descendants(walkable, handle).Where(d => d != handle))
          {
            var viaLabel = WalkerLawProviders.TopologyOf(label).TryGetParent(descendant);
            var viaSource = WalkerLawProviders.TopologyOf(walkable).TryGetParent(descendant);

            Assert.IsTrue(viaLabel.HasParent, $"descendants keep their parents [{tree}]");
            Assert.AreEqual(viaSource.Parent, viaLabel.Parent, $"descendant parents delegate [{tree}]");
          }
        }
      }
    }

    [TestMethod]
    public void CoAssociativity_ASubtreeOfASubtree_IsTheDeeperSubtree()
    {
      foreach (var (tree, _, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        foreach (var handle in walkable.GetHandles())
        {
          var label = WalkerLawProviders.TopologyOf(subtrees).GetValue(handle);

          foreach (var descendant in Descendants(walkable, handle))
          {
            AssertEquivalent(
              WalkerLawProviders.TopologyOf(subtrees).GetValue(descendant),
              WalkerLawProviders.TopologyOf(label.Subtrees()).GetValue(descendant),
              $"duplicate∘duplicate [{tree}]");
          }
        }
      }
    }

    // Interior labels pinned by hand against the preorder ordinal handles the preorder capture
    // assigns (the walkable buffer's guessable-int contract), so the counit tests cannot be
    // green by mutual bug: these expectations were written from the drawings, not the code.
    [TestMethod]
    public void InteriorSubtrees_PinnedByHand()
    {
      var pins = new (string Tree, int Handle, string Expected)[]
      {
        ("a(b(c))", 1, "b(c)"),
        ("a(b(d,e),c(f,g))", 1, "b(d,e)"),
        ("a(b(d,e),c(f,g))", 2, "d"),
        ("a(b(d,e),c(f,g))", 4, "c(f,g)"),
        ("a,b(d),c(e(f))", 4, "e(f)"),
      };

      foreach (var (tree, handle, expected) in pins)
      {
        AssertEquivalent(
          TreeSerializer.DeserializeDepthFirstTree(expected),
          WalkerLawProviders.TopologyOf(W(tree).Subtrees()).GetValue(handle),
          $"interior pin @{handle} [{tree}]");
      }
    }

    [TestMethod]
    public void TheOuterShape_IsTheSourceShape()
    {
      foreach (var (tree, _, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        AssertSameShape(walkable.GetDepthFirstTreenumerator(), subtrees.GetDepthFirstTreenumerator(), $"depth-first [{tree}]");
        AssertSameShape(walkable.GetBreadthFirstTreenumerator(), subtrees.GetBreadthFirstTreenumerator(), $"breadth-first [{tree}]");
      }
    }

    // The reverse door and its round-trip laws: a walker's Subtree() is exactly the label
    // duplicate stamps at its focus, and tree → root walker → Subtree() recovers the tree
    // (the counit in interchange clothing). The other round trip (walker → Subtree() →
    // root walker) deliberately forgets upward context -- severance -- so only this
    // direction is an identity.
    [TestMethod]
    public void TheReverseDoor_AWalkersSubtree_IsDuplicatesLabel()
    {
      foreach (var (tree, rootTrees, walkable) in AllWalkables())
      {
        var subtrees = walkable.Subtrees();

        foreach (var handle in walkable.GetHandles())
        {
          AssertEquivalent(
            WalkerLawProviders.TopologyOf(subtrees).GetValue(handle),
            walkable.GetTreeWalkerAt(handle).Subtree(),
            $"walker.Subtree() ≡ duplicate's label [{tree}]");
        }

        for (var rootIndex = 0; rootIndex < rootTrees.Length; rootIndex++)
        {
          AssertEquivalent(
            TreeSerializer.DeserializeDepthFirstTree(rootTrees[rootIndex]),
            walkable.TryGetTreeWalkerAtRootIndex(rootIndex).Walker.Subtree(),
            $"tree → root walker → Subtree() round trip [{tree}]");
        }
      }
    }

    // ---------------------------------------------------------------------- helpers

    private static IEnumerable<int> Descendants(IWalkableTreenumerable<string, int> source, int handle)
    {
      var pending = new Stack<int>();
      pending.Push(handle);

      while (pending.Count > 0)
      {
        var current = pending.Pop();
        yield return current;

        for (var childIndex = 0; ; childIndex++)
        {
          var childResult = WalkerLawProviders.TopologyOf(source).TryGetChildAt(current, childIndex);

          if (!childResult.HasChild)
            break;

          pending.Push(childResult.Child.Node);
        }
      }
    }

    private static void AssertSameShape(
      ITreenumerator<string> source,
      ITreenumerator<IWalkableTreenumerable<string, int>> outer,
      string context)
    {
      using (source)
      using (outer)
      {
        while (source.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          Assert.IsTrue(outer.MoveNext(NodeTraversalStrategies.TraverseAll), $"outer ended early {context}");
          Assert.AreEqual(source.Mode, outer.Mode, $"mode {context}");
          Assert.AreEqual(source.VisitCount, outer.VisitCount, $"visit count {context}");
          Assert.AreEqual(source.Position, outer.Position, $"position {context}");

          var label = outer.Node;
          Assert.AreEqual(source.Node, WalkerLawProviders.TopologyOf(label).GetValue(WalkerLawProviders.TopologyOf(label).TryGetRootAt(0).Child.Node), $"label root value {context}");
        }

        Assert.IsFalse(outer.MoveNext(NodeTraversalStrategies.TraverseAll), $"outer ran long {context}");
      }
    }

    private static void AssertEquivalent(
      ITreenumerable<string> expected,
      ITreenumerable<string> actual,
      string law)
    {
      CollectionAssert.AreEqual(
        DrainVisits(expected.GetDepthFirstTreenumerator()),
        DrainVisits(actual.GetDepthFirstTreenumerator()),
        $"{law} (depth-first)");

      CollectionAssert.AreEqual(
        DrainVisits(expected.GetBreadthFirstTreenumerator()),
        DrainVisits(actual.GetBreadthFirstTreenumerator()),
        $"{law} (breadth-first)");
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
