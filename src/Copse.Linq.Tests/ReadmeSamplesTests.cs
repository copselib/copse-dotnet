using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Pins the README's samples: every snippet in README.md compiles against the current
  // surface and produces exactly the output the prose claims. When one of these fails, the
  // README is lying to the first person who reads it -- fix the README in the same commit.
  [TestClass]
  public class ReadmeSamplesTests
  {
    // Node n has children 2n and 2n+1 -- a complete binary tree capped at 7.
    private struct BinaryChildren : IChildEnumerator<int>
    {
      private int _next;
      private readonly int _last;
      private bool _disposed;

      public BinaryChildren(int parent)
      {
        _next = parent * 2;
        _last = parent * 2 + 1;
        _disposed = false;
      }

      public Option<HandleAndSiblingIndex<int>> MoveNext()
      {
        if (_disposed || _next > _last || _next > 7)
          return default;

        var child = new HandleAndSiblingIndex<int>(_next, _next % 2);
        _next++;
        return new Option<HandleAndSiblingIndex<int>>(child);
      }

      public void Dispose() => _disposed = true;
    }

    private static ITreenumerable<int> Tree()
      => new HierarchicalTreenumerable<int, BinaryChildren>(
        ctx => new BinaryChildren(ctx.Node), new[] { 1 });

    [TestMethod]
    public void Traversals_and_streaming_operators()
    {
      var tree = Tree();

      CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 3, 6, 7 }, tree.GetPreorderTraversal().ToArray());
      CollectionAssert.AreEqual(new[] { 4, 5, 6, 7 }, tree.GetLeaves().ToArray());

      CollectionAssert.AreEqual(
        new[] { 2, 4, 8, 10, 6, 12, 14 },
        tree.Select(node => node * 2).GetPreorderTraversal().ToArray());

      CollectionAssert.AreEqual(
        new[] { 2, 3 },
        tree.PruneSubtreesWhere((node, position) => position.Depth >= 2).GetLeaves().ToArray());
    }

    [TestMethod]
    public void Where_is_structural()
    {
      CollectionAssert.AreEqual(
        new[] { 1, 5, 3, 7 },
        Tree().Where(node => node % 2 != 0).GetPreorderTraversal().ToArray());
    }

    [TestMethod]
    public void LeaffixAggregate_folds_bottom_up()
    {
      var subtreeSum = Tree()
        .LeaffixAggregate(
          leaf => leaf,
          (accumulate, childAccumulate) => accumulate + childAccumulate,
          (accumulate, node) => accumulate + node)
        .First()
        .Accumulate;

      Assert.AreEqual(28, subtreeSum);
    }

    [TestMethod]
    public void Walker_over_a_capture()
    {
      var capture = Tree().Materialize();
      var walker = capture.GetTreeWalker().MoveToRoot(0).Value;

      Assert.AreEqual(1, walker.GetNode());
      Assert.AreEqual(3, walker.MoveToChild(1).Value.GetNode());
    }

    [TestMethod]
    public void Serializer_round_trip()
    {
      var parsed = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)");

      Assert.AreEqual("a(b(d,e),c)", parsed.SerializeDepthFirstTree());
    }
  }
}
