using Copse.Topologies;
using Copse.Linq.Treenumerables;
using Copse;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The comonad on its reified carrier: TreeWalker is the focused pair as one type, so the
  // laws that WalkerComonadLawTests pins against (walkable, handle) conventions become TYPED
  // IDENTITIES here -- walker.Duplicate().GetValue() is the walker itself, struct-equal, no
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

    private static IEnumerable<(string Tree, IWalkableTreenumerable<string, int> Walkable)> AllWalkables()
      => Trees.SelectMany(tree => WalkerLawProviders.Walkables(tree).Select(walkable => (tree, walkable)));

    [TestMethod]
    public void Extract_IsTheValueAtTheFocus()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(WalkerLawProviders.TopologyOf(walkable).GetValue(handle), walkable.GetTreeWalkerAt(handle).GetValue(), $"extract [{tree}]");
      }
    }

    // The counit, as a typed identity: duplicating and extracting is a no-op, at EVERY focus
    // -- not just the root, which is the understanding this carrier exists to make literal.
    [TestMethod]
    public void Counit_ExtractAfterDuplicate_IsTheWalkerItself()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.GetTreeWalkerAt(handle);

          Assert.AreEqual(walker, walker.Duplicate().GetValue(), $"extract∘duplicate ≡ id [{tree}]");
        }
      }
    }

    // The Store comonad's peek/seek coherence: stepping the duplicated walker and extracting
    // equals stepping the original -- duplicate commutes with navigation, which is what "the
    // labels ARE the refocusings" means operationally.
    [TestMethod]
    public void Duplicate_CommutesWithSteps()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.GetTreeWalkerAt(handle);
          var duplicated = walker.Duplicate();

          var stepped = walker.MoveToChild(0);
          var steppedDuplicated = duplicated.MoveToChild(0);

          Assert.AreEqual(stepped.HasValue, steppedDuplicated.HasValue, $"child step parity [{tree}]");
          if (stepped.HasValue)
            Assert.AreEqual(stepped.Value, steppedDuplicated.Value.GetValue(), $"duplicate commutes with child step [{tree}]");

          var upStepped = walker.MoveToParent();
          var upSteppedDuplicated = duplicated.MoveToParent();

          Assert.AreEqual(upStepped.HasValue, upSteppedDuplicated.HasValue, $"parent step parity [{tree}]");
          if (upStepped.HasValue)
            Assert.AreEqual(upStepped.Value, upSteppedDuplicated.Value.GetValue(), $"duplicate commutes with parent step [{tree}]");
        }
      }
    }

    [TestMethod]
    public void Extend_ExtractRecoversTheObserver()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {

        foreach (var handle in walkable.GetHandles())
        {
          var walker = walkable.GetTreeWalkerAt(handle);
          var extended = walker.Extend(focus => focus.GetValue() + "@" + Depth(focus));

          Assert.AreEqual(walker.GetValue() + "@" + Depth(walker), extended.GetValue(), $"extract∘extend [{tree}]");
        }
      }
    }

    // The vantage is bidirectional -- the Store presentation, pinned on the carrier: a walker
    // below a root can always climb, and sees the same parent the topology reports. (The
    // severed presentation lives in Subtrees(); its labels' roots cannot climb.)
    [TestMethod]
    public void TheWalkerSeesUp()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {

        foreach (var handle in walkable.GetHandles())
        {
          var parentResult = WalkerLawProviders.TopologyOf(walkable).TryGetParent(handle);
          var stepped = walkable.GetTreeWalkerAt(handle).MoveToParent();

          Assert.AreEqual(parentResult.HasValue, stepped.HasValue, $"up-step parity [{tree}]");
          if (parentResult.HasValue)
            Assert.AreEqual(WalkerLawProviders.TopologyOf(walkable).GetValue(parentResult.Value), stepped.Value.GetValue(), $"up-step value [{tree}]");
        }
      }
    }

    [TestMethod]
    public void TheDoors_KeepTheNoUnfocusedInvariant()
    {
      foreach (var walkable in WalkerLawProviders.Walkables("a,b(d),c(e(f))"))
      {
        var firstRoot = walkable.TryGetTreeWalkerAtRootIndex();
        Assert.IsTrue(firstRoot.HasValue);
        Assert.AreEqual("a", firstRoot.Value.GetValue());

        var thirdRoot = walkable.TryGetTreeWalkerAtRootIndex(2);
        Assert.IsTrue(thirdRoot.HasValue);
        Assert.AreEqual("c", thirdRoot.Value.GetValue());

        Assert.IsFalse(walkable.TryGetTreeWalkerAtRootIndex(3).HasValue, "past the last root: no walker, never a walker standing nowhere");
      }
    }

    // The boundary case that forced the carrier split: the empty forest inhabits the
    // walkable type (topology may be empty) but can never yield a comonad value (a walker
    // must stand on an actual node). Both doors refuse honestly -- the root door in its
    // result type, the handle door by never having issued a handle to ask with.
    [TestMethod]
    public void TheEmptyForest_GrantsNoWalker()
    {
      foreach (var provider in new[]
      {
        Tree.Empty<string>().Materialize(BufferLayout.Preorder),
        Tree.Empty<string>().Materialize(BufferLayout.LevelOrder),
        Tree.Empty<string>().Memoize(),
      })
      {
        Assert.IsFalse(provider.TryGetTreeWalkerAtRootIndex().HasValue, "the root door refuses in the result type");
        Assert.IsFalse(provider.GetHandles().Any(), "the handle door never opens: no handle is ever issued");
        Assert.IsFalse(TreeTopology.Lazy(provider).TryGetRootAt(0).HasValue, "no probe succeeds (the deferred door misses honestly)");
      }
    }

    // ---------------------------------------------------------------------- helpers

    private static int Depth(TreeWalker<string, int> walker)
    {
      var depth = 0;
      var stepped = walker.MoveToParent();

      while (stepped.HasValue)
      {
        depth++;
        stepped = stepped.Value.MoveToParent();
      }

      return depth;
    }
  }
}
