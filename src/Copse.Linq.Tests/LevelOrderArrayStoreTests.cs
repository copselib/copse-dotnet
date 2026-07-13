using Copse;
using Copse.Core;
using Copse.Linq.Stores;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The completed level-order array store (PreorderArrayStore's structural dual) and its
  // lazy-built wrapper, decoded through LevelOrderTreenumerable in both dimensions against the
  // serializer's reading of the same tree.
  [TestClass]
  public class LevelOrderArrayStoreTests
  {
    // a(b(d,e),c(f,g)) laid out by hand: values in level order, children contiguous.
    private static LevelOrderArrayStore<string> BranchingStore()
      => new LevelOrderArrayStore<string>(
        new[] { "a", "b", "c", "d", "e", "f", "g" },
        new[] { 1, 3, 5, 0, 0, 0, 0 },
        new[] { 2, 2, 2, 0, 0, 0, 0 },
        rootCount: 1);

    [TestMethod]
    public void ServesBothDimensions()
    {
      var tree = new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(BranchingStore());
      var expected = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c(f,g))");

      foreach (var strategy in new[] { TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.DepthFirst })
        CollectionAssert.AreEqual(
          expected.GetTraversal(strategy).ToArray(),
          tree.GetTraversal(strategy).ToArray(),
          strategy.ToString());
    }

    [TestMethod]
    public void ServesAForest()
    {
      var store = new LevelOrderArrayStore<string>(
        new[] { "a", "b", "c" },
        new[] { 2, 0, 0 },
        new[] { 1, 0, 0 },
        rootCount: 2);

      var tree = new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(store);
      var expected = TreeSerializer.DeserializeDepthFirstTree("a(c),b");

      foreach (var strategy in new[] { TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.DepthFirst })
        CollectionAssert.AreEqual(
          expected.GetTraversal(strategy).ToArray(),
          tree.GetTraversal(strategy).ToArray(),
          strategy.ToString());
    }

    [TestMethod]
    public void ServesTheEmptyForest()
    {
      var store = new LevelOrderArrayStore<string>(
        new string[0], new int[0], new int[0], rootCount: 0);

      var tree = new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(store);

      Assert.AreEqual(0, tree.GetTraversal(TreeTraversalStrategy.BreadthFirst).Count());
      Assert.AreEqual(0, tree.GetTraversal(TreeTraversalStrategy.DepthFirst).Count());
    }

    [TestMethod]
    public void LazyBuiltDualBuildsOnceAcrossDimensions()
    {
      var builds = 0;

      var store = new LazyBuiltLevelOrderStore<string>(() =>
      {
        builds++;
        return BranchingStore();
      });

      var tree = new LevelOrderTreenumerable<string, LazyBuiltLevelOrderStore<string>>(store);

      Assert.AreEqual(0, builds, "the build must wait for the first pull");

      tree.GetTraversal(TreeTraversalStrategy.BreadthFirst).ToArray();
      tree.GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray();

      Assert.AreEqual(1, builds, "the build runs once, serving both dimensions");
    }
  }
}
