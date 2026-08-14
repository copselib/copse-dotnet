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
        if (predicate(row.Value))
          handles.Add(row.Handle);

      return handles;
    }

    [TestMethod]
    public async Task SpanningSubtree_TheWholeArc_AndBothMisses()
    {
      var walkable = W("a(b(d(h,i),e),c(f,g(j)))");

      var targets = await HandlesWhereAsync(walkable, value => value == "h" || value == "i" || value == "g");
      var spanning = await walkable.SpanningSubtreeAsync(targets);

      Assert.IsTrue(spanning.HasWalker);
      Assert.AreEqual("a", await spanning.Walker.GetValueAsync(), "the walker stands at the spanning root");
      CollectionAssert.AreEqual(
        new[] { "a", "b", "d", "h", "i", "c", "g" },
        await PreorderValuesAsync(spanning.Walker.Subtree()),
        "the spanning subtree, preorder");

      // The misses are facts, same as the sync twins pin: no targets, and disjoint trees.
      Assert.IsFalse((await walkable.SpanningSubtreeAsync(Enumerable.Empty<int>())).HasWalker, "k = 0 is an honest miss");

      var forest = W("a(b),c(d)");
      var disjointTargets = await HandlesWhereAsync(forest, value => value == "b" || value == "d");
      Assert.IsFalse((await forest.SpanningSubtreeAsync(disjointTargets)).HasWalker, "disjoint targets: an honest miss");
    }

    [TestMethod]
    public async Task PruneAfterLens_IsAPairCitizen()
    {
      var walkable = W("a(b(d,e),c)");
      var pruned = walkable.PruneAfter(value => value == "b");

      // The adjacency half: a pruned-after node hands out no children; everything else delegates.
      var handleOfB = (await HandlesWhereAsync(walkable, value => value == "b")).Single();
      Assert.IsFalse((await pruned.TryGetChildAtAsync(handleOfB, 0)).HasChild, "b keeps no children");
      Assert.IsTrue((await pruned.TryGetParentAsync(handleOfB)).HasParent, "b keeps its ancestry");

      // The order half: the streaming operator, wholesale.
      CollectionAssert.AreEqual(new[] { "a", "b", "c" }, await PreorderValuesAsync(pruned));
    }
  }
}
