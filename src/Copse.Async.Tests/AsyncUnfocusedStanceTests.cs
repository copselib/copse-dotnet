using Copse.Async;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the unfocused stance in this color (the semantic surface is pinned
  // once, on the generated sync twins, by UnfocusedStanceTests): the total door, the climb
  // topping out, the typed miss, and the hoist round trip must run end to end over a
  // genuinely async walkable -- a narrow-source memo whose probes drive the capture.
  [TestClass]
  public class AsyncUnfocusedStanceTests
  {
    private static IAsyncWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(tree)).Memoize();

    private static async Task<List<string>> PreorderValuesAsync(IAsyncTreenumerable<string> source)
    {
      var values = new List<string>();

      await foreach (var value in source.GetPreorderTraversal())
        values.Add(value);

      return values;
    }

    [TestMethod]
    public async Task TheDoor_TheClimb_TheTypedMiss_AndTheHoist()
    {
      var walkable = W("a(b(d,e),c)");
      var door = await walkable.GetTreeWalkerAsync();

      Assert.IsFalse(door.HasFocus, "the door lands on the unfocused stance");
      Assert.IsFalse((await door.TryGetValueAsync()).HasValue, "the unfocused walker's value read is the typed miss");

      // Down to d through the unfocused stance's child group, then the climb back past the top.
      var nodeD = (await (await (await door.MoveToChildAsync(0)).Value.MoveToChildAsync(0)).Value.MoveToChildAsync(0)).Value;
      Assert.AreEqual("d", await nodeD.GetValueAsync());

      var nodeB = (await nodeD.MoveToParentAsync()).Value;
      var nodeA = (await nodeB.MoveToParentAsync()).Value;
      var top = (await nodeA.MoveToParentAsync()).Value;

      Assert.AreEqual("b", await nodeB.GetValueAsync());
      Assert.AreEqual("a", await nodeA.GetValueAsync());
      Assert.IsFalse(top.HasFocus, "a root's parent is the unfocused stance");
      Assert.IsFalse((await top.MoveToParentAsync()).HasValue, "stepping up from the unfocused stance is the one upward miss");

      // The hoist round trip, forest included: door then Subtree() is the source.
      var forest = W("a(b),c(d)");
      CollectionAssert.AreEqual(
        new[] { "a", "b", "c", "d" },
        await PreorderValuesAsync((await forest.GetTreeWalkerAsync()).Subtree()),
        "hoist at the unfocused stance: the whole forest");
    }

    [TestMethod]
    public async Task TheEmptyForest_IsTheUnfocusedStanceAlone()
    {
      var empty = W("a").Where(value => false).Materialize(BufferLayout.Preorder);
      var door = await empty.GetTreeWalkerAsync();

      Assert.IsFalse(door.HasFocus, "born inhabited: the empty forest's comonad side is one stance");
      Assert.IsFalse((await door.MoveToChildAsync(0)).HasValue, "with an empty child group");
    }
  }
}
