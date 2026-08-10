using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Materialize is DEFERRED (2026-08-10, the lazy-Materialize law): construction is uniformly
  // lazy -- the whole capture runs at the first pull -- and the pin is a commitment made at the
  // earliest moment it is free: the organic overload's first consumer pins the layout at that
  // first pull; the strategy overload's pin lands at the call (zero nodes pulled). These tests
  // cover that surface plus the consume policies. The shared replay machinery -- dimension
  // buffers, serving rule, pruning, concurrency -- is covered by MemoizeTests.
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
          .GetTraversal(strategy, node => node == "a" ? NodeTraversalStrategies.SkipDescendants : NodeTraversalStrategies.TraverseAll)
          .Where(visit => visit.Mode == TreenumeratorMode.SchedulingNode)
          .Select(visit => visit.Node)
          .ToList();

        CollectionAssert.AreEqual(new[] { "a" }, scheduled, $"SkipDescendants not honored ({strategy})");
      }
    }

    [TestMethod]
    public void Materialize_defers_the_whole_capture_to_the_first_pull()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      var materialized = counting.Materialize();

      // Nothing opens at the call: an unconsumed result holds exactly what the unconsumed
      // pipeline already held.
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations);

      // The first pull runs the whole capture -- one source enumeration, in the dimension the
      // first consumer asked for.
      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(1, counting.DepthFirstEnumerations);

      // Replays ride the capture; the source is never touched again, in either dimension.
      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      materialized.GetTraversal(TreeTraversalStrategy.BreadthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
    }

    // The lazy-Materialize law's organic half: THE FIRST CONSUMER PINS THE LAYOUT. Under the
    // eager regime a plain source always captured preorder; now a breadth-first-first consumer
    // gets a level-order capture, and the other dimension replays cross-order from it.
    [TestMethod]
    public void Materialize_first_consumer_pins_the_layout()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      var materialized = counting.Materialize();

      materialized.GetTraversal(TreeTraversalStrategy.BreadthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "breadth-first-first captures level-order");

      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();
      Assert.AreEqual(0, counting.DepthFirstEnumerations, "the other dimension replays cross-order from the one capture");
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
    }

    [TestMethod]
    public void Materialize_completes_the_pinned_capture_at_the_first_pull()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));
      var memo = counting.Memoize();

      using (var bfs = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 6; i++)
          Assert.IsTrue(bfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      var materialized = memo.Materialize();

      // The settle waits for the first pull; until then the memo's sunk work sits where it was.
      Assert.IsFalse(memo.IsComplete);

      materialized.GetTraversal(TreeTraversalStrategy.BreadthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();

      // The first pull completed the pinned capture IN BULK: the sunk BFT work was finished
      // and the feed retired; the DFT dimension was never opened.
      Assert.IsTrue(memo.IsComplete);
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

      // The layout guarantee: the strategy is never ignored, and the wrapper reports it from
      // the call onward -- while the work waits for the first pull.
      Assert.AreNotSame(memo, materialized);
      Assert.AreEqual(BufferLayout.Preorder, materialized.NativeLayout);
      Assert.IsFalse(memo.IsComplete);

      materialized.GetTraversal(TreeTraversalStrategy.DepthFirst, _ => NodeTraversalStrategies.TraverseAll).Count();

      // The first pull's settle: the pinned level-order capture completes (the one source
      // enumeration -- at-most-once holds), then TRANSPOSES from the buffer into a new
      // preorder-native capture; the source is untouched by the transpose.
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

      // The wrapper reports the guaranteed layout from the call, so the compliant re-probe
      // reuses it -- a capture is never re-captured -- and the pin pulled zero nodes.
      Assert.AreEqual(BufferLayout.LevelOrder, first.NativeLayout);
      Assert.AreSame(first, again, "a compliant buffer is never re-captured");
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations, "the pin pulls zero nodes");

      first.Consume(TreeTraversalStrategy.BreadthFirst);
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "the settle fills the capture the call pinned");
    }

    // The both-layouts recipe for speed-over-space callers: materialize once, then materialize
    // THAT in the other dimension -- two native-layout buffers, ONE source enumeration.
    [TestMethod]
    public void Both_layouts_cost_one_source_enumeration()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      var levelOrder = counting.Materialize(TreeTraversalStrategy.BreadthFirst);
      var preorder = levelOrder.Materialize(TreeTraversalStrategy.DepthFirst);

      // Both deferrals stack without touching anything: the transpose's first pull forces the
      // capture's first pull, one source enumeration total, buffer-to-buffer from there.
      Assert.AreNotSame(levelOrder, preorder);
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations, "nothing opens before the first pull");

      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"), TreeTraversalStrategy.DepthFirst),
        Collect(preorder, TreeTraversalStrategy.DepthFirst));

      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "the transpose walks the buffer, never the source");
    }

    // The lazy-Materialize law's strategy half: THE PIN LANDS AT THE CALL, because the call is
    // when it is free -- so an intervening consumer of the shared memo cannot pin it the other
    // way between the Materialize call and its first pull.
    [TestMethod]
    public void Materialize_with_declared_strategy_pins_the_shared_memo_at_the_call()
    {
      var counting = new CountingSource(TreeSerializer.DeserializeDepthFirstTree("a(b(d,e,f),c(g,h,i))"));

      using var memo = counting.Memoize();
      var materialized = memo.Materialize(TreeTraversalStrategy.BreadthFirst);

      Assert.IsFalse(memo.IsComplete, "the work waits for the first pull");
      Assert.AreEqual(0, counting.DepthFirstEnumerations + counting.BreadthFirstEnumerations, "the pin pulls zero nodes");

      // The intervening consumer's depth-first drain rides the level-order capture the call
      // pinned, cross-order -- it cannot re-pin the memo.
      memo.Consume(TreeTraversalStrategy.DepthFirst);
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations, "the capture the call pinned is the one that fills");

      // The settle then finds the memo already complete and compliant: no transpose.
      materialized.Consume(TreeTraversalStrategy.BreadthFirst);
      Assert.IsTrue(memo.IsComplete);
      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
    }

    // The buffer probes: Materialize never re-captures a capture. Probe order matters -- the
    // lazy buffer interface derives from the completed one, so it is tested first (a live memo
    // must be settled, not returned raw).
    [TestMethod]
    public void Materialize_wraps_a_live_memo_and_completes_it_at_the_first_pull()
    {
      using var memo = TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c)").Memoize();

      var materialized = memo.Materialize();

      // The result is the non-disposable completion seam over the memo -- not the memo itself,
      // whose disposal stays the caller's.
      Assert.AreNotSame(memo, materialized);
      Assert.IsFalse(memo.IsComplete);

      materialized.Consume();

      Assert.IsTrue(memo.IsComplete, "the first pull completes the memo's capture in bulk");
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
