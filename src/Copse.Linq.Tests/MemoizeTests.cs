using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class MemoizeTests
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

    // A rich single tree for the strategy matrix: internal nodes with siblings on both sides.
    private const string RichTree = "a(b(d,e,f),c(g,h,i))";

    // ---------------------------------------------------------------------------------------
    // Oracle: a memoized replay's visit stream is identical to traversing the source directly.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Memoized_matches_source_DepthFirst()
      => AssertSameTraversal(TreeTraversalStrategy.DepthFirst);

    [TestMethod]
    public void Memoized_matches_source_BreadthFirst()
      => AssertSameTraversal(TreeTraversalStrategy.BreadthFirst);

    private static void AssertSameTraversal(TreeTraversalStrategy strategy)
    {
      foreach (var tree in Trees)
      {
        var source = TreeSerializer.DeserializeDepthFirstTree(tree);
        var memoized = TreeSerializer.DeserializeDepthFirstTree(tree).Memoize();

        CollectionAssert.AreEqual(
          Collect(source, strategy),
          Collect(memoized, strategy),
          $"{strategy} traversal mismatch for {tree}");

        // A second replay serves from the now-complete capture (case 1) -- same stream.
        CollectionAssert.AreEqual(
          Collect(source, strategy),
          Collect(memoized, strategy),
          $"{strategy} second replay mismatch for {tree}");
      }
    }

    [TestMethod]
    public void Memoized_preserves_structure()
    {
      foreach (var tree in Trees)
      {
        var memoized = TreeSerializer.DeserializeDepthFirstTree(tree).Memoize();
        Assert.AreEqual(tree, memoized.SerializeDepthFirstTree(), $"structure mismatch for {tree}");
      }
    }

    [TestMethod]
    public void Empty_tree_replays_empty_and_completes()
    {
      var memo = TreeSerializer.DeserializeDepthFirstTree("").Memoize();

      Assert.AreEqual(0, Collect(memo, TreeTraversalStrategy.DepthFirst).Count);
      Assert.IsTrue(memo.IsComplete);
      Assert.AreEqual(0, Collect(memo, TreeTraversalStrategy.BreadthFirst).Count);
    }

    // ---------------------------------------------------------------------------------------
    // The four-case serving rule, observed through source enumeration counts.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void BFS_after_full_DFS_never_touches_the_source_again()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();

      var directDfs = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.DepthFirst);
      var directBfs = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.BreadthFirst);

      CollectionAssert.AreEqual(directDfs, Collect(memo, TreeTraversalStrategy.DepthFirst));

      Assert.IsTrue(memo.IsComplete);

      // Cross-order serving (case 2): the BFS engine rides the completed preorder capture.
      CollectionAssert.AreEqual(directBfs, Collect(memo, TreeTraversalStrategy.BreadthFirst));

      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
      Assert.AreEqual(0, memo.GetBufferedCount(TreeTraversalStrategy.BreadthFirst));
    }

    [TestMethod]
    public void DFS_after_full_BFS_never_touches_the_source_again()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();

      var directDfs = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.DepthFirst);
      var directBfs = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.BreadthFirst);

      CollectionAssert.AreEqual(directBfs, Collect(memo, TreeTraversalStrategy.BreadthFirst));

      Assert.IsTrue(memo.IsComplete);

      CollectionAssert.AreEqual(directDfs, Collect(memo, TreeTraversalStrategy.DepthFirst));

      Assert.AreEqual(0, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
      Assert.AreEqual(0, memo.GetBufferedCount(TreeTraversalStrategy.DepthFirst));
    }

    [TestMethod]
    public void Partial_then_deeper_enumeration_extends_one_shared_feed()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();
      var total = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.DepthFirst);

      // Stop early: only a prefix of the tree is captured (laziness), the feed suspends.
      using (var replay = memo.GetDepthFirstTreenumerator())
        for (var i = 0; i < 4; i++)
          Assert.IsTrue(replay.MoveNext(NodeTraversalStrategies.TraverseAll));

      var buffered = memo.GetBufferedCount(TreeTraversalStrategy.DepthFirst);
      Assert.IsTrue(buffered > 0 && buffered < 9, $"expected a strict prefix, buffered {buffered} of 9");
      Assert.IsFalse(memo.IsComplete);

      // A deeper enumeration resumes the same suspended feed rather than re-running the source.
      CollectionAssert.AreEqual(total, Collect(memo, TreeTraversalStrategy.DepthFirst));
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
    }

    [TestMethod]
    public void Both_dimensions_partial_run_independent_feeds_and_stay_correct()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();

      using (var dfs = memo.GetDepthFirstTreenumerator())
        for (var i = 0; i < 3; i++)
          Assert.IsTrue(dfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      using (var bfs = memo.GetBreadthFirstTreenumerator())
        for (var i = 0; i < 3; i++)
          Assert.IsTrue(bfs.MoveNext(NodeTraversalStrategies.TraverseAll));

      // Case 4: partial work in BOTH dimensions is the one road to a second source enumeration.
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);

      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.DepthFirst),
        Collect(memo, TreeTraversalStrategy.DepthFirst));
      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.BreadthFirst),
        Collect(memo, TreeTraversalStrategy.BreadthFirst));

      // Still one enumeration per dimension: the partial feeds were resumed, not restarted.
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
    }

    // ---------------------------------------------------------------------------------------
    // Consumer pruning on replay: every strategy, both dimensions, against fresh and completed
    // captures (fresh exercises fill-under-pruning, e.g. the DFT skip-hop; completed exercises
    // the native and cross-order serving paths).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Replay_honors_consumer_strategies_in_all_serving_modes()
    {
      var strategies = new[]
      {
        NodeTraversalStrategies.SkipNode,
        NodeTraversalStrategies.SkipDescendants,
        NodeTraversalStrategies.SkipSiblings,
        NodeTraversalStrategies.SkipNodeAndDescendants,
      };
      var targets = new[] { "a", "b", "d" }; // root, internal, leaf

      foreach (var traversal in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        foreach (var strategy in strategies)
          foreach (var target in targets)
          {
            var expected = CollectPruned(TreeSerializer.DeserializeDepthFirstTree(RichTree), traversal, target, strategy);

            // Fresh memo: the replay's pruning drives lazy fills (cases 3-4).
            var fresh = TreeSerializer.DeserializeDepthFirstTree(RichTree).Memoize();
            CollectionAssert.AreEqual(
              expected,
              CollectPruned(fresh, traversal, target, strategy),
              $"fresh memo: {traversal}, {strategy} at {target}");

            // Captures completed in each dimension: native (case 1) and cross-order (case 2) serving.
            foreach (var captured in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
            {
              var consumed = TreeSerializer.DeserializeDepthFirstTree(RichTree).Memoize();
              consumed.Consume(captured);
              CollectionAssert.AreEqual(
                expected,
                CollectPruned(consumed, traversal, target, strategy),
                $"capture {captured}: {traversal}, {strategy} at {target}");
            }
          }
    }

    // ---------------------------------------------------------------------------------------
    // Memoize/Materialize/Consume surface semantics.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Memoize_on_a_buffer_is_identity()
    {
      var memo = TreeSerializer.DeserializeDepthFirstTree(RichTree).Memoize();
      Assert.AreSame(memo, memo.Memoize());
    }

    [TestMethod]
    public void Consume_is_a_noop_once_any_dimension_is_complete()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();

      memo.Consume(TreeTraversalStrategy.DepthFirst);
      Assert.IsTrue(memo.IsComplete);

      // The invariant outranks the argument: a retired source is never re-enumerated.
      memo.Consume(TreeTraversalStrategy.BreadthFirst);

      Assert.AreEqual(1, counting.DepthFirstEnumerations);
      Assert.AreEqual(0, counting.BreadthFirstEnumerations);
      Assert.AreEqual(0, memo.GetBufferedCount(TreeTraversalStrategy.BreadthFirst));
    }

    // ---------------------------------------------------------------------------------------
    // Concurrent replays over one shared capture: an interleaved pair must each see the exact
    // direct-traversal stream. The leader pulls the feed at the frontier; the trailer reads
    // already-buffered slots -- the append-only/monotonic safety claim, exercised by
    // leapfrogging. Uneven stepping (2:1) forces lead changes.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Two_interleaved_DFS_replays_both_match()
      => AssertInterleavedPair(TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.DepthFirst, expectedDepthFirstEnumerations: 1, expectedBreadthFirstEnumerations: 0);

    [TestMethod]
    public void Two_interleaved_BFS_replays_both_match()
      => AssertInterleavedPair(TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.BreadthFirst, expectedDepthFirstEnumerations: 0, expectedBreadthFirstEnumerations: 1);

    [TestMethod]
    public void Interleaved_DFS_and_BFS_replays_both_match()
      => AssertInterleavedPair(TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst, expectedDepthFirstEnumerations: 1, expectedBreadthFirstEnumerations: 1);

    private static void AssertInterleavedPair(
      TreeTraversalStrategy strategyA,
      TreeTraversalStrategy strategyB,
      int expectedDepthFirstEnumerations,
      int expectedBreadthFirstEnumerations)
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();

      var collectedA = new List<(TreenumeratorMode, int, int, int, string)>();
      var collectedB = new List<(TreenumeratorMode, int, int, int, string)>();

      using (var a = memo.GetTreenumerator(strategyA))
      using (var b = memo.GetTreenumerator(strategyB))
      {
        bool liveA = true, liveB = true;

        // A takes two steps to B's one until A finishes, then B drains: B trails, then leads.
        while (liveA || liveB)
        {
          for (var i = 0; i < 2 && liveA; i++)
            liveA = Step(a, collectedA);

          if (liveB)
            liveB = Step(b, collectedB);
        }
      }

      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), strategyA),
        collectedA,
        $"replay A ({strategyA}) mismatch");
      CollectionAssert.AreEqual(
        Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), strategyB),
        collectedB,
        $"replay B ({strategyB}) mismatch");

      // Same-dimension pairs share one feed; a mixed pair runs one feed per dimension.
      Assert.AreEqual(expectedDepthFirstEnumerations, counting.DepthFirstEnumerations);
      Assert.AreEqual(expectedBreadthFirstEnumerations, counting.BreadthFirstEnumerations);
    }

    private static bool Step(
      ITreenumerator<string> treenumerator,
      List<(TreenumeratorMode, int, int, int, string)> into)
    {
      if (!treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        return false;

      into.Add((treenumerator.Mode, treenumerator.Position.Depth, treenumerator.Position.SiblingIndex, treenumerator.VisitCount, treenumerator.Node));
      return true;
    }

    // ---------------------------------------------------------------------------------------
    // Drop-on-completion and disposal.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Straggler_replay_finishes_on_its_own_feed_after_the_other_dimension_completes()
    {
      var counting = new CountingTreenumerable<string>(TreeSerializer.DeserializeDepthFirstTree(RichTree));
      var memo = counting.Memoize();
      var expected = Collect(TreeSerializer.DeserializeDepthFirstTree(RichTree), TreeTraversalStrategy.DepthFirst);

      var collected = new List<(TreenumeratorMode, int, int, int, string)>();
      using (var straggler = memo.GetDepthFirstTreenumerator())
      {
        for (var i = 0; i < 4; i++)
        {
          Assert.IsTrue(straggler.MoveNext(NodeTraversalStrategies.TraverseAll));
          collected.Add((straggler.Mode, straggler.Position.Depth, straggler.Position.SiblingIndex, straggler.VisitCount, straggler.Node));
        }

        // The other dimension completes mid-flight; the straggler's buffer takes no new
        // customers but stays alive for the straggler itself -- no cut-over, no fault.
        memo.Consume(TreeTraversalStrategy.BreadthFirst);
        Assert.IsTrue(memo.IsComplete);

        while (straggler.MoveNext(NodeTraversalStrategies.TraverseAll))
          collected.Add((straggler.Mode, straggler.Position.Depth, straggler.Position.SiblingIndex, straggler.VisitCount, straggler.Node));
      }

      CollectionAssert.AreEqual(expected, collected);
      Assert.AreEqual(1, counting.DepthFirstEnumerations);

      // New DFS requests cross-serve from the completed BFT capture; the straggler's feed was
      // its own affair.
      CollectionAssert.AreEqual(expected, Collect(memo, TreeTraversalStrategy.DepthFirst));
      Assert.AreEqual(1, counting.DepthFirstEnumerations);
    }

    [TestMethod]
    public void Disposed_memo_serves_the_captured_region_then_throws_at_the_frontier()
    {
      var memo = TreeSerializer.DeserializeDepthFirstTree(RichTree).Memoize();

      using (var replay = memo.GetDepthFirstTreenumerator())
        for (var i = 0; i < 5; i++)
          Assert.IsTrue(replay.MoveNext(NodeTraversalStrategies.TraverseAll));

      var buffered = memo.GetBufferedCount(TreeTraversalStrategy.DepthFirst);
      Assert.IsTrue(buffered > 0 && buffered < 9);

      memo.Dispose();

      // The captured region still replays; the first pull past the frontier throws.
      using (var replay = memo.GetDepthFirstTreenumerator())
      {
        var visits = 0;
        try
        {
          while (replay.MoveNext(NodeTraversalStrategies.TraverseAll))
            visits++;
          Assert.Fail("expected ObjectDisposedException at the capture frontier");
        }
        catch (ObjectDisposedException)
        {
          Assert.IsTrue(visits > 0, "expected the captured region to replay before the frontier");
        }
      }
    }

    // ---------------------------------------------------------------------------------------
    // Unbounded sources: a memoized traversal terminates whenever the direct one does.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Infinite_tree_BFS_replays_lazily_and_extends_on_demand()
    {
      var counting = new CountingTreenumerable<int>(InfiniteBinaryTree());
      var memo = counting.Memoize();

      CollectionAssert.AreEqual(
        CollectPrefix(InfiniteBinaryTree(), TreeTraversalStrategy.BreadthFirst, 50),
        CollectPrefix(memo, TreeTraversalStrategy.BreadthFirst, 50));

      // A deeper prefix resumes the same feed.
      CollectionAssert.AreEqual(
        CollectPrefix(InfiniteBinaryTree(), TreeTraversalStrategy.BreadthFirst, 200),
        CollectPrefix(memo, TreeTraversalStrategy.BreadthFirst, 200));

      Assert.AreEqual(1, counting.BreadthFirstEnumerations);
      Assert.IsFalse(memo.IsComplete);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------

    private static List<(TreenumeratorMode, int, int, int, T)> Collect<T>(
      ITreenumerable<T> tree,
      TreeTraversalStrategy strategy)
    {
      var result = new List<(TreenumeratorMode, int, int, int, T)>();
      using (var t = tree.GetTreenumerator(strategy))
        while (t.MoveNext(NodeTraversalStrategies.TraverseAll))
          result.Add((t.Mode, t.Position.Depth, t.Position.SiblingIndex, t.VisitCount, t.Node));
      return result;
    }

    private static List<(TreenumeratorMode, int, int, int, T)> CollectPrefix<T>(
      ITreenumerable<T> tree,
      TreeTraversalStrategy strategy,
      int count)
    {
      var result = new List<(TreenumeratorMode, int, int, int, T)>();
      using (var t = tree.GetTreenumerator(strategy))
        while (result.Count < count && t.MoveNext(NodeTraversalStrategies.TraverseAll))
          result.Add((t.Mode, t.Position.Depth, t.Position.SiblingIndex, t.VisitCount, t.Node));
      return result;
    }

    private static List<(TreenumeratorMode, int, int, int, string)> CollectPruned(
      ITreenumerable<string> tree,
      TreeTraversalStrategy traversal,
      string target,
      NodeTraversalStrategies strategy)
      => tree
        .GetTraversal(traversal, nc => nc.Node == target ? strategy : NodeTraversalStrategies.TraverseAll)
        .Select(v => (v.Mode, v.Position.Depth, v.Position.SiblingIndex, v.VisitCount, v.Node))
        .ToList();

    // The infinite binary tree: node n has children 2n+1 and 2n+2. Infinite depth, so only
    // bounded (prefix) consumption terminates -- exactly like the direct traversal.
    private static ITreenumerable<int> InfiniteBinaryTree()
      => new Treenumerable<int, InfiniteBinaryChildEnumerator>(
        nodeContext => new InfiniteBinaryChildEnumerator(nodeContext.Node),
        new[] { 0 });

    private struct InfiniteBinaryChildEnumerator : IChildEnumerator<int>
    {
      public InfiniteBinaryChildEnumerator(int parent)
      {
        _Parent = parent;
        _SiblingIndex = 0;
        _Disposed = false;
      }

      private readonly int _Parent;
      private int _SiblingIndex;
      private bool _Disposed;

      public ChildResult<int> MoveNext()
      {
        if (_Disposed || _SiblingIndex >= 2)
          return default;

        var child = new NodeAndSiblingIndex<int>(2 * _Parent + 1 + _SiblingIndex, _SiblingIndex);
        _SiblingIndex++;
        return new ChildResult<int>(child);
      }

      public void Dispose() => _Disposed = true;
    }

    // Counts how many times each dimension of the source is enumerated -- the observable the
    // four-case serving rule is specified in terms of.
    private sealed class CountingTreenumerable<T> : ITreenumerable<T>
    {
      public CountingTreenumerable(ITreenumerable<T> inner) => _Inner = inner;

      private readonly ITreenumerable<T> _Inner;

      public int DepthFirstEnumerations { get; private set; }
      public int BreadthFirstEnumerations { get; private set; }

      public ITreenumerator<T> GetDepthFirstTreenumerator()
      {
        DepthFirstEnumerations++;
        return _Inner.GetDepthFirstTreenumerator();
      }

      public ITreenumerator<T> GetBreadthFirstTreenumerator()
      {
        BreadthFirstEnumerations++;
        return _Inner.GetBreadthFirstTreenumerator();
      }
    }
  }
}
