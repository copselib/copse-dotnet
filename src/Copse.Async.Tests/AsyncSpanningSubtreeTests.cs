using Copse.Async;
using Copse.Core.Async;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Copse.Async.Tests
{
  // Thin MECHANICS check for the async spanning arc and the async PruneAfter lens (the
  // semantic surface is pinned once, on the generated sync twins, by
  // SpanningSubtreeScenarioTests and PruneAfterLensTests): the async spellings must run the
  // same arc end to end over a genuinely async walkable -- a narrow-source memo whose probes
  // drive the capture (pull-through).
  [TestClass]
  public class AsyncSpanningSubtreeTests
  {
    // The SPI seam (Stage C): coherence checks reach the bound topology through the door.
    private static async ValueTask<IAsyncTreeTopology<string, int>> TopologyOf(IAsyncWalkableTreenumerable<string, int> walkable)
      => (await walkable.GetTreeWalkerAsync()).Topology;

    private static IAsyncWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTreeAsync(() => new StringReader(tree)).Memoize();

    private static async Task<List<string>> PreorderValuesAsync(IAsyncWalkableTreenumerable<string, int> source)
    {
      var values = new List<string>();

      await foreach (var value in source.GetPreorderTraversal())
        values.Add(value);

      return values;
    }

    private static async Task<List<int>> HandlesWhereAsync(
      IAsyncWalkableTreenumerable<string, int> walkable,
      System.Func<string, bool> predicate)
    {
      // The rowid idiom, async spelling: rows in, value predicate, handles out.
      var handles = new List<int>();

      await foreach (var row in walkable.GetHandlesWithValues())
        if (predicate(row.Node))
          handles.Add(row.Handle);

      return handles;
    }

    [TestMethod]
    public async Task SpanningSubtree_TheWholeArc_AndBothMisses()
    {
      var walkable = W("a(b(d(h,i),e),c(f,g(j)))");

      var targets = await HandlesWhereAsync(walkable, value => value == "h" || value == "i" || value == "g");
      var spanning = await walkable.SpanningSubtreeAsync(targets);

      Assert.IsTrue(spanning.HasValue);
      Assert.AreEqual("a", await spanning.Value.GetValueAsync(), "the walker stands at the spanning root");
      CollectionAssert.AreEqual(
        new[] { "a", "b", "d", "h", "i", "c", "g" },
        await PreorderValuesAsync(spanning.Value.Subtree()),
        "the spanning subtree, preorder");

      // The one miss left is a fact, same as the sync twins pin: no targets. Disjoint
      // trees ANSWER now -- their common ancestor is the unfocused stance.
      Assert.IsFalse((await walkable.SpanningSubtreeAsync(Enumerable.Empty<int>())).HasValue, "k = 0 is an honest miss");

      var forest = W("a(b),c(d)");
      var disjointTargets = await HandlesWhereAsync(forest, value => value == "b" || value == "d");
      var disjoint = await forest.SpanningSubtreeAsync(disjointTargets);

      Assert.IsTrue(disjoint.HasValue, "disjoint targets answer");
      Assert.IsFalse(disjoint.Value.HasFocus, "the spanning of disjoint targets stands at the unfocused stance");
      CollectionAssert.AreEqual(
        new[] { "a", "b", "c", "d" },
        await PreorderValuesAsync(disjoint.Value.Subtree()),
        "the spanning forest under the unfocused walker, hoisted");
    }

    [TestMethod]
    public async Task PruneAfterLens_IsAPairCitizen()
    {
      var walkable = W("a(b(d,e),c)");
      var pruned = walkable.PruneAfter(value => value == "b");

      // The adjacency half: a pruned-after node hands out no children; everything else delegates.
      var handleOfB = (await HandlesWhereAsync(walkable, value => value == "b")).Single();
      Assert.IsFalse((await (await TopologyOf(pruned)).TryGetChildAtAsync(handleOfB, 0)).HasValue, "b keeps no children");
      Assert.IsTrue((await (await TopologyOf(pruned)).TryGetParentAsync(handleOfB)).HasValue, "b keeps its ancestry");

      // The order half: the streaming operator, wholesale.
      CollectionAssert.AreEqual(new[] { "a", "b", "c" }, await PreorderValuesAsync(pruned));
    }
  }
}
