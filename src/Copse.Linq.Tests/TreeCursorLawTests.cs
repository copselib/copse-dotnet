using Copse;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The comonad on its reified carrier: TreeCursor is the focused pair as one type, so the
  // laws that WalkerComonadLawTests pins against (walkable, handle) conventions become TYPED
  // IDENTITIES here -- cursor.Duplicate().Value is the cursor itself, struct-equal, no
  // stream-draining needed. (Extend's deep laws -- co-associativity over neighborhood
  // observers -- are inherited: cursor.Extend delegates to the walkable Extend those tests
  // pin; this suite pins what the CARRIER adds: the counit as an equality, steps commuting
  // with duplicate, the observer receiving a genuine cursor, and the doors' no-unfocused
  // invariant.)
  [TestClass]
  public class TreeCursorLawTests
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
          Assert.AreEqual(walkable.GetValue(handle), walkable.CursorAt(handle).Value, $"extract [{tree}]");
      }
    }

    // The counit, as a typed identity: duplicating and extracting is a no-op, at EVERY focus
    // -- not just the root, which is the understanding this carrier exists to make literal.
    [TestMethod]
    public void Counit_ExtractAfterDuplicate_IsTheCursorItself()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var cursor = walkable.CursorAt(handle);

          Assert.AreEqual(cursor, cursor.Duplicate().Value, $"extract∘duplicate ≡ id [{tree}]");
        }
      }
    }

    // The Store comonad's peek/seek coherence: stepping the duplicated cursor and extracting
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
          var cursor = walkable.CursorAt(handle);
          var duplicated = cursor.Duplicate();

          var stepped = cursor.MoveToChild(0);
          var steppedDuplicated = duplicated.MoveToChild(0);

          Assert.AreEqual(stepped.HasCursor, steppedDuplicated.HasCursor, $"child step parity [{tree}]");
          if (stepped.HasCursor)
            Assert.AreEqual(stepped.Cursor, steppedDuplicated.Cursor.Value, $"duplicate commutes with child step [{tree}]");

          var upStepped = cursor.MoveToParent();
          var upSteppedDuplicated = duplicated.MoveToParent();

          Assert.AreEqual(upStepped.HasCursor, upSteppedDuplicated.HasCursor, $"parent step parity [{tree}]");
          if (upStepped.HasCursor)
            Assert.AreEqual(upStepped.Cursor, upSteppedDuplicated.Cursor.Value, $"duplicate commutes with parent step [{tree}]");
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
          var cursor = walkable.CursorAt(handle);
          var extended = cursor.Extend(focus => focus.Value + "@" + Depth(focus));

          Assert.AreEqual(cursor.Value + "@" + Depth(cursor), extended.Value, $"extract∘extend [{tree}]");
        }
      }
    }

    // The vantage is bidirectional -- the Store presentation, pinned on the carrier: a cursor
    // below a root can always climb, and sees the same parent the terrain reports. (The
    // severed presentation lives in Subtrees(); its labels' roots cannot climb.)
    [TestMethod]
    public void TheCursorSeesUp()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        foreach (var handle in walkable.GetHandles())
        {
          var parentResult = walkable.GetParent(handle);
          var stepped = walkable.CursorAt(handle).MoveToParent();

          Assert.AreEqual(parentResult.HasParent, stepped.HasCursor, $"up-step parity [{tree}]");
          if (parentResult.HasParent)
            Assert.AreEqual(walkable.GetValue(parentResult.Parent), stepped.Cursor.Value, $"up-step value [{tree}]");
        }
      }
    }

    [TestMethod]
    public void TheDoors_KeepTheNoUnfocusedInvariant()
    {
      var walkable = W("a,b(d),c(e(f))");

      var firstRoot = walkable.GetRootCursor();
      Assert.IsTrue(firstRoot.HasCursor);
      Assert.AreEqual("a", firstRoot.Cursor.Value);

      var thirdRoot = walkable.GetRootCursor(2);
      Assert.IsTrue(thirdRoot.HasCursor);
      Assert.AreEqual("c", thirdRoot.Cursor.Value);

      Assert.IsFalse(walkable.GetRootCursor(3).HasCursor, "past the last root: no cursor, never a cursor standing nowhere");
    }

    // ---------------------------------------------------------------------- helpers

    private static int Depth(TreeCursor<string, int> cursor)
    {
      var depth = 0;
      var stepped = cursor.MoveToParent();

      while (stepped.HasCursor)
      {
        depth++;
        stepped = stepped.Cursor.MoveToParent();
      }

      return depth;
    }
  }
}
