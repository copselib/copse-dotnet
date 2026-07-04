using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Materialize is eager Memoize (Memoize + Consume), so these tests cover the EAGER surface:
  // capture-at-call-time semantics and the consume policies. The shared replay machinery --
  // dimension buffers, serving rule, pruning, concurrency -- is covered by MemoizeTests.
  [TestClass]
  public class MaterializeTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a,b(c)",
      "a(b,c,d)",
      "a(b(d(e)),c)",
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
      "a,b(d),c(e(f))",
    };

    [TestMethod]
    public void Materialized_preserves_structure()
    {
      foreach (var tree in Trees)
      {
        var materialized = TreeSerializer.Deserialize(tree).Materialize();
        Assert.AreEqual(tree, materialized.Serialize(), $"structure mismatch for {tree}");
      }
    }

    [TestMethod]
    public void Materialized_matches_source_DepthFirst()
      => AssertSameTraversal(TreeTraversalStrategy.DepthFirst);

    [TestMethod]
    public void Materialized_matches_source_BreadthFirst()
      => AssertSameTraversal(TreeTraversalStrategy.BreadthFirst);

    private static void AssertSameTraversal(TreeTraversalStrategy strategy)
    {
      foreach (var tree in Trees)
      {
        var source = TreeSerializer.Deserialize(tree);
        var materialized = TreeSerializer.Deserialize(tree).Materialize();

        CollectionAssert.AreEqual(
          Collect(source, strategy),
          Collect(materialized, strategy),
          $"{strategy} traversal mismatch for {tree}");
      }
    }

    // Guards the IChildEnumerator contract that PreorderChildEnumerator must honor: the engine
    // signals SkipDescendants by Disposing the child enumerator, so a disposed enumerator must
    // yield no further children. (A no-op Dispose silently ignores all skip strategies.)
    [TestMethod]
    public void Materialized_honors_SkipDescendants()
    {
      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        var tree = TreeSerializer.Deserialize("a(b,c)").Materialize();

        var scheduled =
          tree
          .GetTraversal(strategy, nc => nc.Node == "a" ? NodeTraversalStrategies.SkipDescendants : NodeTraversalStrategies.TraverseAll)
          .Where(visit => visit.Mode == TreenumeratorMode.SchedulingNode)
          .Select(visit => visit.Node)
          .ToList();

        CollectionAssert.AreEqual(new[] { "a" }, scheduled, $"SkipDescendants not honored ({strategy})");
      }
    }

    [TestMethod]
    public void Materialize_captures_eagerly_at_the_call()
    {
      var counting = new CountingSource(TreeSerializer.Deserialize("a(b(d,e,f),c(g,h,i))"));

      var materialized = counting.Materialize();

      // The whole capture happened inside the call -- before any replay exists.
      Assert.IsTrue(materialized.IsComplete);
      Assert.AreEqual(1, counting.DepthFirstEnumerations);

      // Replays ride the capture; the source is never touched again, in either dimension.
      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      materialized.GetTraversal(TreeTraversalStrategy.BreadthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
    }

    [TestMethod]
    public void Materialize_finishes_the_most_buffered_dimension()
    {
      var counting = new CountingSource(TreeSerializer.Deserialize("a(b(d,e,f),c(g,h,i))"));
      var memo = counting.Memoize();

      using (var bfs = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 6; i++)
          Assert.IsTrue(bfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      var materialized = memo.Materialize();

      Assert.AreSame(memo, materialized);
      Assert.IsTrue(memo.IsComplete);

      // The sunk BFT work was finished; the DFT dimension was never opened.
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
      Assert.AreEqual(9, memo.GetBufferedCount(TreeTraversalStrategy.BreadthFirst));
    }

    [TestMethod]
    public void Materialize_with_declared_strategy_outranks_sunk_cost()
    {
      var counting = new CountingSource(TreeSerializer.Deserialize("a(b(d,e,f),c(g,h,i))"));
      var memo = counting.Memoize();

      using (var bfs = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 6; i++)
          Assert.IsTrue(bfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      var materialized = memo.Materialize(TreeTraversalStrategy.DepthFirst);

      Assert.AreSame(memo, materialized);
      Assert.IsTrue(memo.IsComplete);
      Assert.AreEqual(9, memo.GetBufferedCount(TreeTraversalStrategy.DepthFirst));

      // Declared intent paid for a second enumeration, and the partial BFT capture was dropped.
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
      Assert.AreEqual(0, memo.GetBufferedCount(TreeTraversalStrategy.BreadthFirst));
    }

    private sealed class CountingSource : ITreenumerable<string>
    {
      public CountingSource(ITreenumerable<string> inner) => _Inner = inner;

      private readonly ITreenumerable<string> _Inner;

      public int DepthFirstEnumerations { get; private set; }
      public int BreadthFirstEnumerations { get; private set; }

      public ITreenumerator<string> GetDepthFirstTreenumerator()
      {
        DepthFirstEnumerations++;
        return _Inner.GetDepthFirstTreenumerator();
      }

      public ITreenumerator<string> GetBreadthFirstTreenumerator()
      {
        BreadthFirstEnumerations++;
        return _Inner.GetBreadthFirstTreenumerator();
      }
    }

    private static List<(TreenumeratorMode, int, int, int, string)> Collect(
      ITreenumerable<string> tree,
      TreeTraversalStrategy strategy)
    {
      var result = new List<(TreenumeratorMode, int, int, int, string)>();
      using (var t = tree.GetTreenumerator(strategy))
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          result.Add((t.Mode, t.Position.Depth, t.Position.SiblingIndex, t.VisitCount, t.Node));
      return result;
    }
  }
}
