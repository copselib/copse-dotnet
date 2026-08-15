using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the async walker (the comonad's laws are pinned once, on the
  // generated sync twins, by the walker law suites and their provider fan-out): the async
  // carrier must extract, step, extend with ASYNC observers, duplicate to a typed-identity
  // counit, and keep the no-unfocused invariant at the doors -- exercised over a genuinely
  // async walkable, a narrow-source memo whose probes drive the capture (pull-through).
  [TestClass]
  public class AsyncTreeWalkerLawTests
  {
    // The SPI seam (Stage C): coherence checks reach the bound topology through the door.
    private static async ValueTask<IAsyncTreeTopology<string, int>> TopologyOf(IAsyncWalkableTreenumerable<string, int> walkable)
      => (await walkable.TryGetTreeWalkerAsync()).Walker.Topology;

    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
    };

    private static IAsyncWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(tree)).Memoize();

    [TestMethod]
    public async Task Extract_IsTheValueAtTheFocus()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        await foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(
            await (await TopologyOf(walkable)).GetValueAsync(handle),
            await (await walkable.GetTreeWalkerAtAsync(handle)).GetValueAsync(),
            $"extract [{tree}]");
      }
    }

    [TestMethod]
    public async Task Counit_ExtractAfterDuplicate_IsTheWalkerItself()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        await foreach (var handle in walkable.GetHandles())
        {
          var walker = (await walkable.GetTreeWalkerAtAsync(handle));

          Assert.AreEqual(walker, await walker.Duplicate().GetValueAsync(), $"extract∘duplicate ≡ id [{tree}]");
        }
      }
    }

    [TestMethod]
    public async Task TheWalkerSeesUp()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        await foreach (var handle in walkable.GetHandles())
        {
          var parentResult = await (await TopologyOf(walkable)).TryGetParentAsync(handle);
          var stepped = await (await walkable.GetTreeWalkerAtAsync(handle)).MoveToParentAsync();

          Assert.AreEqual(parentResult.HasParent, stepped.HasWalker, $"up-step parity [{tree}]");
          if (parentResult.HasParent)
            Assert.AreEqual(
              await (await TopologyOf(walkable)).GetValueAsync(parentResult.Parent),
              await stepped.Walker.GetValueAsync(),
              $"up-step value [{tree}]");
        }
      }
    }

    // The OPEN-10 shape, exercised: the observer is an ASYNC arrow -- it probes, probes are
    // async in this color, and the transcriber collapses the arrow for the sync twin.
    [TestMethod]
    public async Task Extend_WithAnAsyncObserver_ExtractRecoversIt()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        await foreach (var handle in walkable.GetHandles())
        {
          var walker = (await walkable.GetTreeWalkerAtAsync(handle));
          var extended = walker.Extend(async focus => await focus.GetValueAsync().ConfigureAwait(false) + "@" + focus.Focus);

          Assert.AreEqual(
            await walker.GetValueAsync() + "@" + handle,
            await extended.GetValueAsync(),
            $"extract∘extend [{tree}]");
        }
      }
    }

    [TestMethod]
    public async Task TheDoors_KeepTheNoUnfocusedInvariant()
    {
      var forest = W("a,b,c");

      var firstRoot = await forest.TryGetTreeWalkerAtRootIndexAsync();
      Assert.IsTrue(firstRoot.HasWalker);
      Assert.AreEqual("a", await firstRoot.Walker.GetValueAsync());

      Assert.IsFalse((await forest.TryGetTreeWalkerAtRootIndexAsync(3)).HasWalker, "past the last root: no walker");

      var empty = AsyncTree.Empty<string>().Memoize();
      Assert.IsFalse((await empty.TryGetTreeWalkerAtRootIndexAsync()).HasWalker, "the empty forest grants no walker");
    }

    [TestMethod]
    public async Task TheReverseDoor_SubtreeStandsAtTheFocus_Severed()
    {
      var walkable = W("a(b(c))");

      await foreach (var handle in walkable.GetHandles())
      {
        var subtree = (await walkable.GetTreeWalkerAtAsync(handle)).Subtree();

        var subtreeRoot = await subtree.TryGetTreeWalkerAtRootIndexAsync();
        Assert.IsTrue(subtreeRoot.HasWalker);
        Assert.AreEqual(handle, subtreeRoot.Walker.Focus, "the subtree stands at the focus");

        Assert.IsFalse((await (await TopologyOf(subtree)).TryGetParentAsync(handle)).HasParent, "severed at the root");
      }
    }
  }
}
