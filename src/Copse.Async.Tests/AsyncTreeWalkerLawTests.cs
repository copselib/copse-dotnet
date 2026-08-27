using Copse;
using Copse.Treenumerables;
using Copse.Core;
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
  // counit, and honor the root door's bounds -- exercised over a genuinely async walkable,
  // a narrow-source memo whose probes drive the capture (pull-through). The unfocused stance's
  // async mechanics live in AsyncUnfocusedStanceTests.
  [TestClass]
  public class AsyncTreeWalkerLawTests
  {
    // The SPI seam (Stage C): coherence checks reach the bound topology through the door.
    private static async ValueTask<IAsyncTreeTopology<string, int>> TopologyOf(IAsyncWalkableTreenumerable<string, int> walkable)
      => (await walkable.GetTreeWalkerAsync()).Topology;

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
            await (await TopologyOf(walkable)).GetNodeAsync(handle),
            await (await walkable.GetTreeWalkerAtAsync(handle)).GetNodeAsync(),
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

          Assert.AreEqual(walker, await walker.Duplicate().GetNodeAsync(), $"extract∘duplicate ≡ id [{tree}]");
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

          // Up-steps from nodes always answer: the probe's miss at a root is the step's
          // unfocused stance -- the climb tops out standing above the roots.
          Assert.IsTrue(stepped.HasValue, $"up-step answers from every node [{tree}]");
          Assert.AreEqual(parentResult.HasValue, stepped.Value.HasFocus, $"probe miss <=> unfocused stance [{tree}]");
          if (parentResult.HasValue)
            Assert.AreEqual(
              await (await TopologyOf(walkable)).GetNodeAsync(parentResult.Value),
              await stepped.Value.GetNodeAsync(),
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
          var extended = walker.Extend(async focus => await focus.GetNodeAsync().ConfigureAwait(false) + "@" + focus.Focus);

          Assert.AreEqual(
            await walker.GetNodeAsync() + "@" + handle,
            await extended.GetNodeAsync(),
            $"extract∘extend [{tree}]");
        }
      }
    }

    [TestMethod]
    public async Task TheRootDoor_AnswersInBounds_MissesPastTheLastRoot()
    {
      var forest = W("a,b,c");

      var firstRoot = await forest.TryGetTreeWalkerAtRootIndexAsync();
      Assert.IsTrue(firstRoot.HasValue);
      Assert.AreEqual("a", await firstRoot.Value.GetNodeAsync());

      Assert.IsFalse((await forest.TryGetTreeWalkerAtRootIndexAsync(3)).HasValue, "past the last root: no walker");

      var empty = AsyncTree.Empty<string>().Memoize();
      Assert.IsFalse((await empty.TryGetTreeWalkerAtRootIndexAsync()).HasValue, "the empty forest's root door misses: no focused walker to grant");
    }

    [TestMethod]
    public async Task TheReverseDoor_SubtreeStandsAtTheFocus_Severed()
    {
      var walkable = W("a(b(c))");

      await foreach (var handle in walkable.GetHandles())
      {
        var subtree = (await walkable.GetTreeWalkerAtAsync(handle)).Subtree();

        var subtreeRoot = await subtree.TryGetTreeWalkerAtRootIndexAsync();
        Assert.IsTrue(subtreeRoot.HasValue);
        Assert.AreEqual(handle, subtreeRoot.Value.Focus, "the subtree stands at the focus");

        Assert.IsFalse((await (await TopologyOf(subtree)).TryGetParentAsync(handle)).HasValue, "severed at the root");
      }
    }
  }
}
