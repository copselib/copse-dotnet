using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // TakeLastTrees / SkipLastTrees: two-pass terminal-builders (count the roots, then take/skip)
  // -- and CountTrees, the counting pass itself. The narrow-dimension entries must agree with
  // the full-citizen entry across the whole count range, boundaries included.
  [TestClass]
  public class LastTreesTests
  {
    private static readonly string[] Forests =
    {
      "a",
      "a,b,c",
      "a(b,c),d",
      "a,b(d),c(e(f))",
      "a(d(g)),b(e(h)),c(f(i))",
    };

    [TestMethod]
    public void TakeLastTrees_keeps_the_last_count_roots()
    {
      var forest = TreeSerializer.DeserializeDepthFirstTree("a,b(d),c(e(f))");

      CollectionAssert.AreEqual(
        new[] { "b", "d", "c", "e", "f" },
        forest.TakeLastTrees(2).PreorderTraversal().ToList());
    }

    [TestMethod]
    public void SkipLastTrees_drops_the_last_count_roots()
    {
      var forest = TreeSerializer.DeserializeDepthFirstTree("a,b(d),c(e(f))");

      CollectionAssert.AreEqual(
        new[] { "a" },
        forest.SkipLastTrees(2).PreorderTraversal().ToList());
    }

    [TestMethod]
    public void Boundary_counts_take_everything_or_nothing()
    {
      var forest = TreeSerializer.DeserializeDepthFirstTree("a,b,c");

      Assert.AreEqual(0, forest.TakeLastTrees(0).CountTrees());
      Assert.AreEqual(3, forest.TakeLastTrees(5).CountTrees());
      Assert.AreEqual(3, forest.SkipLastTrees(0).CountTrees());
      Assert.AreEqual(0, forest.SkipLastTrees(5).CountTrees());
    }

    [TestMethod]
    public void CountTrees_agrees_across_dimensions()
    {
      foreach (var forest in Forests)
      {
        var full = TreeSerializer.DeserializeDepthFirstTree(forest);

        Assert.AreEqual(
          full.CountTrees(),
          ((IBreadthFirstTreenumerable<string>)full).CountTrees(),
          $"breadth-first count disagrees for {forest}");
      }
    }

    [TestMethod]
    public void TakeLastTrees_narrow_entries_agree_with_the_full_entry()
    {
      foreach (var forest in Forests)
        for (var count = 0; count <= 4; count++)
        {
          var expected = Full(forest).TakeLastTrees(count);

          CollectionAssert.AreEqual(
            expected.PreorderTraversal().ToList(),
            ((IDepthFirstTreenumerable<string>)Full(forest)).TakeLastTrees(count).PreorderTraversal().ToList(),
            $"depth-first narrow disagrees for {forest} taking {count}");

          CollectionAssert.AreEqual(
            expected.LevelOrderTraversal().ToList(),
            ((IBreadthFirstTreenumerable<string>)Full(forest)).TakeLastTrees(count).LevelOrderTraversal().ToList(),
            $"breadth-first narrow disagrees for {forest} taking {count}");
        }
    }

    [TestMethod]
    public void SkipLastTrees_narrow_entries_agree_with_the_full_entry()
    {
      foreach (var forest in Forests)
        for (var count = 0; count <= 4; count++)
        {
          var expected = Full(forest).SkipLastTrees(count);

          CollectionAssert.AreEqual(
            expected.PreorderTraversal().ToList(),
            ((IDepthFirstTreenumerable<string>)Full(forest)).SkipLastTrees(count).PreorderTraversal().ToList(),
            $"depth-first narrow disagrees for {forest} skipping {count}");

          CollectionAssert.AreEqual(
            expected.LevelOrderTraversal().ToList(),
            ((IBreadthFirstTreenumerable<string>)Full(forest)).SkipLastTrees(count).LevelOrderTraversal().ToList(),
            $"breadth-first narrow disagrees for {forest} skipping {count}");
        }
    }

    private static ITreenumerable<string> Full(string forest)
      => TreeSerializer.DeserializeDepthFirstTree(forest);
  }
}
