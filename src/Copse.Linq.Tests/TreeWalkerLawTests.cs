using Copse;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The comonad on its reified carrier: TreeWalker is the focused pair as one type, so the
  // laws that WalkerComonadLawTests pins against (walkable, handle) conventions become TYPED
  // IDENTITIES here -- walker.Duplicate().Value is the walker itself, struct-equal, no
  // stream-draining needed. (Extend's deep laws -- co-associativity over neighborhood
  // observers -- are inherited: walker.Extend delegates to the walkable Extend those tests
  // pin; this suite pins what the CARRIER adds: the counit as an equality, steps commuting
  // with duplicate, the observer receiving a genuine walker, and the doors' no-unfocused
  // invariant.)
  [TestClass]
  public class TreeWalkerLawTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a,b(d),c(e(f))",
    };

    private static IWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree).MaterializeWalkable();

    [TestMethod]
    public void Extract_IsTheValueAtTheFocus()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(walkable.GetValue(handle), walkable.WalkerAt(handle).Value, $"extract [{tree}]");
      }
    }

    // The counit, as a typed identity: duplicating and extracting is a no-op, at EVERY focus
    // -- not just the root, which is the understanding this carrier exists to make literal.
    [TestMethod]
    public void Counit_ExtractAfterDuplicate_IsTheWalkerItself()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.WalkerAt(handle);

          Assert.AreEqual(walker, walker.Duplicate().Value, $"extract∘duplicate ≡ id [{tree}]");
        }
      }
    }

    // The Store comonad's peek/seek coherence: stepping the duplicated walker and extracting
    // equals stepping the original -- duplicate commutes with navigation, which is what "the
    // labels ARE the refocusings" means operationally.
    [TestMethod]
    public void Duplicate_CommutesWithSteps()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.WalkerAt(handle);
          var duplicated = walker.Duplicate();

          var stepped = walker.MoveToChild(0);
          var steppedDuplicated = duplicated.MoveToChild(0);

          Assert.AreEqual(stepped.HasWalker, steppedDuplicated.HasWalker, $"child step parity [{tree}]");
          if (stepped.HasWalker)
            Assert.AreEqual(stepped.Walker, steppedDuplicated.Walker.Value, $"duplicate commutes with child step [{tree}]");

          var upStepped = walker.MoveToParent();
          var upSteppedDuplicated = duplicated.MoveToParent();

          Assert.AreEqual(upStepped.HasWalker, upSteppedDuplicated.HasWalker, $"parent step parity [{tree}]");
          if (upStepped.HasWalker)
            Assert.AreEqual(upStepped.Walker, upSteppedDuplicated.Walker.Value, $"duplicate commutes with parent step [{tree}]");
        }
      }
    }

    [TestMethod]
    public void Extend_ExtractRecoversTheObserver()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.WalkerAt(handle);
          var extended = walker.Extend(focus => focus.Value + "@" + Depth(focus));

          Assert.AreEqual(walker.Value + "@" + Depth(walker), extended.Value, $"extract∘extend [{tree}]");
        }
      }
    }

    // The vantage is bidirectional -- the Store presentation, pinned on the carrier: a walker
    // below a root can always climb, and sees the same parent the terrain reports. (The
    // severed presentation lives in Subtrees(); its labels' roots cannot climb.)
    [TestMethod]
    public void TheWalkerSeesUp()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var parentResult = walkable.GetParent(handle);
          var stepped = walkable.WalkerAt(handle).MoveToParent();

          Assert.AreEqual(parentResult.HasParent, stepped.HasWalker, $"up-step parity [{tree}]");
          if (parentResult.HasParent)
            Assert.AreEqual(walkable.GetValue(parentResult.Parent), stepped.Walker.Value, $"up-step value [{tree}]");
        }
      }
    }

    [TestMethod]
    public void TheDoors_KeepTheNoUnfocusedInvariant()
    {
      var walkable = W("a,b(d),c(e(f))");

      var firstRoot = walkable.GetRootWalker();
      Assert.IsTrue(firstRoot.HasWalker);
      Assert.AreEqual("a", firstRoot.Walker.Value);

      var thirdRoot = walkable.GetRootWalker(2);
      Assert.IsTrue(thirdRoot.HasWalker);
      Assert.AreEqual("c", thirdRoot.Walker.Value);

      Assert.IsFalse(walkable.GetRootWalker(3).HasWalker, "past the last root: no walker, never a walker standing nowhere");
    }

    // The boundary case that forced the carrier split: the empty forest inhabits the
    // walkable type (terrain may be empty) but can never yield a comonad value (a walker
    // must stand on an actual node). Both doors refuse honestly -- the root door in its
    // result type, the handle door by never having issued a handle to ask with.
    [TestMethod]
    public void TheEmptyForest_GrantsNoWalker()
    {
      var empty = Tree.Empty<string>().MaterializeWalkable();

      Assert.IsFalse(empty.GetRootWalker().HasWalker, "the root door refuses in the result type");
      Assert.IsFalse(empty.GetHandles().Any(), "the handle door never opens: no handle is ever issued");
      Assert.IsFalse(empty.GetRootAt(0).HasChild, "no probe succeeds");
    }

    // ---------------------------------------------------------------------- helpers

    private static int Depth(TreeWalker<string, int> walker)
    {
      var depth = 0;
      var stepped = walker.MoveToParent();

      while (stepped.HasWalker)
      {
        depth++;
        stepped = stepped.Walker.MoveToParent();
      }

      return depth;
    }
  }
}
