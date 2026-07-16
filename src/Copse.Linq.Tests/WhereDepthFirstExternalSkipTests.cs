using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Linq.Tests
{
  /// <summary>
  /// Regression tests for the WhereDepthFirstTreenumerator crash that occurred
  /// when the consumer drove the treenumerator with an external SkipNode
  /// strategy (e.g. via AnyNodes(..., DepthFirst), which passes SkipNode on
  /// every MoveNext). The very first MoveNext(SkipNode) used to pop the sentinel
  /// off _NodeVisits, leaving the accepted stack empty, so the next
  /// GetStackWithDeepestNodeVisit() threw "The stack is empty."
  /// </summary>
  [TestClass]
  public class WhereDepthFirstExternalSkipTests
  {
    // PruneBefore is implemented as a WhereDepthFirstTreenumerator with
    // SkipNodeAndDescendants. AnyNodes(DepthFirst) feeds it SkipNode externally.
    [TestMethod]
    public void PruneBefore_AnyNodesFalse_DepthFirst_DoesNotThrow()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c),d(e))");

      var result =
        tree
          .PruneBefore(ctx => ctx.Position.Depth == 2)
          .AnyNodes(_ => false, TreeTraversalStrategy.DepthFirst);

      Assert.IsFalse(result);
    }

    [TestMethod]
    public void PruneBefore_AnyNodesFalse_DepthFirst_DeepTree_DoesNotThrow()
    {
      // Single deep chain; prune before depth 2 leaves only a(b).
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c(d(e(f)))))");

      var result =
        tree
          .PruneBefore(ctx => ctx.Position.Depth == 2)
          .AnyNodes(_ => false, TreeTraversalStrategy.DepthFirst);

      Assert.IsFalse(result);
    }

    // After pruning before depth 2, nodes at depth 0 and 1 survive.
    // AnyNodes for a surviving node must return true.
    [TestMethod]
    public void PruneBefore_AnyNodesMatchesSurvivingNode_DepthFirst_ReturnsTrue()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c),d(e))");

      var result =
        tree
          .PruneBefore(ctx => ctx.Position.Depth == 2)
          .AnyNodes(ctx => ctx.Node == "d", TreeTraversalStrategy.DepthFirst);

      Assert.IsTrue(result);
    }

    // A node that is pruned away must NOT be found.
    [TestMethod]
    public void PruneBefore_AnyNodesMatchesPrunedNode_DepthFirst_ReturnsFalse()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c),d(e))");

      var result =
        tree
          .PruneBefore(ctx => ctx.Position.Depth == 2)
          .AnyNodes(ctx => ctx.Node == "c", TreeTraversalStrategy.DepthFirst);

      Assert.IsFalse(result);
    }

    // Direct user-level Where consumed with an external SkipNode strategy
    // (AnyNodes DepthFirst). Where(true) keeps every node, so any present node
    // must be findable and a never-matching predicate must return false.
    [TestMethod]
    public void Where_AnyNodes_DepthFirst_ExternalSkip_DoesNotThrow()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c),d(e))");

      var foundExisting =
        tree
          .Where(_ => true)
          .AnyNodes(ctx => ctx.Node == "e", TreeTraversalStrategy.DepthFirst);

      var foundMissing =
        tree
          .Where(_ => true)
          .AnyNodes(_ => false, TreeTraversalStrategy.DepthFirst);

      Assert.IsTrue(foundExisting);
      Assert.IsFalse(foundMissing);
    }

    // Where that filters an internal node (child promotion) consumed with an
    // external SkipNode strategy must still locate promoted descendants.
    [TestMethod]
    public void Where_FilterInternalNode_AnyNodes_DepthFirst_FindsPromotedChild()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)");

      // Remove b; c and d are promoted under a.
      var pruned = tree.Where(ctx => ctx != "b");

      Assert.IsTrue(pruned.AnyNodes(ctx => ctx.Node == "c", TreeTraversalStrategy.DepthFirst));
      Assert.IsTrue(pruned.AnyNodes(ctx => ctx.Node == "d", TreeTraversalStrategy.DepthFirst));
      Assert.IsFalse(pruned.AnyNodes(ctx => ctx.Node == "b", TreeTraversalStrategy.DepthFirst));
    }
  }
}
