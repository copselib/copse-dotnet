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
        var materialized = TreeSerializer.DeserializeDepthFirstTree(tree).Materialize();
        Assert.AreEqual(tree, materialized.SerializeDepthFirstTree(), $"structure mismatch for {tree}");
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
        var source = TreeSerializer.DeserializeDepthFirstTree(tree);
        var materialized = TreeSerializer.DeserializeDepthFirstTree(tree).Materialize();

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
        var tree = TreeSerializer.DeserializeDepthFirstTree("a(b,c)").Materialize();

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
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      var materialized = counting.Materialize();

      // The whole capture happened inside the call -- the source was enumerated once, before any
      // replay exists. (Completeness is now the return type's contract, proven below: a partial
      // capture could not serve full replays without re-touching the source.)
      Assert.AreEqual(1, counting.DepthFirstEnumerations);

      // Replays ride the capture; the source is never touched again, in either dimension.
      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      materialized.GetTraversal(TreeTraversalStrategy.BreadthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
    }

    [TestMethod]
    public void Materialize_completes_the_pinned_capture()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));
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
      Assert.AreEqual(9, memo.GetBufferedCount());
    }

    [TestMethod]
    public void Materialize_with_declared_strategy_transposes_a_mismatched_pin_from_the_buffer()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));
      var memo = counting.Memoize();

      using (var bfs = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 6; i++)
          Assert.IsTrue(bfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      var materialized = memo.Materialize(TreeTraversalStrategy.DepthFirst);

      // The layout guarantee: the strategy is never ignored. The pinned level-order capture
      // completes (the one source enumeration -- at-most-once holds), then TRANSPOSES from the
      // buffer into a new preorder-native capture; the source is untouched by the transpose.
      Assert.AreNotSame(memo, materialized);
      Assert.IsTrue(memo.IsComplete);
      Assert.AreEqual(9, memo.GetBufferedCount());
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);

      // The transposed buffer replays the same tree in both dimensions.
      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"), TreeTraversalStrategy.DepthFirst),
        Collect(materialized, TreeTraversalStrategy.DepthFirst));
      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"), TreeTraversalStrategy.BreadthFirst),
        Collect(materialized, TreeTraversalStrategy.BreadthFirst));
    }

    [TestMethod]
    public void Materialize_with_matching_strategy_reuses_the_buffer()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));
      using var memo = counting.Memoize();

      var first = memo.Materialize(TreeTraversalStrategy.BreadthFirst);
      var again = first.Materialize(TreeTraversalStrategy.BreadthFirst);

      Assert.AreSame(memo, first, "a fresh memo takes the declared layout as its pin");
      Assert.AreSame(first, again, "a compliant buffer is never re-captured");
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
    }

    // The both-layouts recipe for speed-over-space callers: materialize once, then materialize
    // THAT in the other dimension -- two native-layout buffers, ONE source enumeration.
    [TestMethod]
    public void Both_layouts_cost_one_source_enumeration()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      var levelOrder = counting.Materialize(TreeTraversalStrategy.BreadthFirst);
      var preorder = levelOrder.Materialize(TreeTraversalStrategy.DepthFirst);

      Assert.AreNotSame(levelOrder, preorder);
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "the transpose walks the buffer, never the source");

      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"), TreeTraversalStrategy.DepthFirst),
        Collect(preorder, TreeTraversalStrategy.DepthFirst));
    }

    [TestMethod]
    public void Materialize_with_declared_strategy_pins_a_fresh_memo()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      using var memo = counting.Memoize();
      memo.Materialize(TreeTraversalStrategy.BreadthFirst);

      Assert.IsTrue(memo.IsComplete);
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "a fresh memo takes the declared layout as its pin");
    }

    // The buffer probes: Materialize never re-captures a capture. Probe order matters -- the
    // lazy buffer interface derives from the completed one, so it is tested first (a live memo
    // must be consumed, not returned raw).
    [TestMethod]
    public void Materialize_consumes_a_live_memo_in_place()
    {
      using var memo = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Memoize();

      var materialized = memo.Materialize();

      Assert.AreSame(memo, materialized);
      Assert.IsTrue(memo.IsComplete);
    }

    [TestMethod]
    public void Materialize_returns_a_completed_buffer_as_is()
    {
      var buffer = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Materialize();

      Assert.AreSame(buffer, buffer.Materialize());
    }

    // Invert's result is a deferred capture (a pinned lazy build behind the buffer type);
    // Materialize hands it back untouched -- the build is pinned either way, so eagerness
    // gains nothing and re-capturing would copy every node.
    [TestMethod]
    public void Materialize_returns_a_deferred_capture_as_is()
    {
      var mirror = TreeSerializer.DeserializeDepthFirstTree("a(b,c)").Invert();

      Assert.AreSame(mirror, mirror.Materialize());
    }

    // Consume is MECHANICAL -- it walks anything, buffers included. The pins below say what
    // that means for captures: a completed buffer replays without touching the source (inert),
    // a deferred capture is FORCED by the walk, and a fresh memo's capture completes as a side
    // effect of being walked. Minimum-work settling is Complete()/Materialize's job.
    [TestMethod]
    public void Consume_walks_a_completed_buffer_without_touching_the_source()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)"));

      var buffer = counting.Materialize();

      buffer.Consume();
      buffer.Consume(TreeTraversalStrategy.BreadthFirst);

      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations, "the walks replay the inert capture; the source is retired");
    }

    [TestMethod]
    public void Consume_forces_a_deferred_capture_by_walking_it()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b,c)"));

      var mirror = counting.Invert();
      mirror.Consume();

      Assert.AreEqual(1, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations,
        "the walk runs the pinned build -- exactly what a test reaching for Consume wants");

      mirror.Consume();

      Assert.AreEqual(1, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations,
        "the build ran at most once; further walks replay the inert capture");
    }

    [TestMethod]
    public void Consume_completes_a_lazy_buffer_as_a_side_effect_of_the_walk()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)"));

      using var memo = counting.Memoize();

      ITreenumerable<string> plain = memo;
      plain.Consume();

      Assert.IsTrue(memo.IsComplete, "walking a fresh memo to exhaustion completes its capture");
      Assert.AreEqual(1, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations);
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
