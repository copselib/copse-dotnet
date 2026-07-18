using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Tests
{
  // The load-bearing core invariant: the breadth-first and depth-first treenumerators must
  // emit the SAME (Mode, Node, VisitCount, Position) visit multiset for ANY assignment of
  // traversal strategies -- just in a different order. DFT is the trusted oracle, so any
  // divergence is a BFT bug.
  //
  // This is an EXHAUSTIVE in-process scan (not a sampled DynamicData fuzzer): for every tree
  // it tries every assignment of up to K concurrent (node, strategy) skips, where K is the
  // largest concurrency that fits PerTreeBudget -- i.e. FULL exhaustion (every strategy on
  // every node) for small trees, and capped concurrency for large ones. The cap stays well
  // above the 4 concurrent skips the Where wrapper can feed (<=2 filter + <=2 consumer).
  //
  // Bugs A/B/C were within 2 concurrent skips; Bug D needed 3. Undiscovered core bugs make
  // the combinatorial Where oracle (and any hand-built wrapper test) untrustworthy, so the core is pinned
  // down exhaustively here first.
  [TestClass]
  public class DepthFirstVsBreadthFirstTests
  {
    private static readonly string[] TreeStrings = new[]
    {
      // single / multi-root, varying width & depth
      "a,b,c", "a(b,c)", "a(b(c))", "a(b),c(d)", "a(b(c,d))",
      "a(b,c),d(e,f)", "a(b(c)),d(e(f))", "a,b(d),c(e(f))", "b(d),c(f)", "b(d),e(f)",
      "a,b(c)", "a(b,c,d)", "a,b(c,d)", "a,b(d),c",
      // combinatorial-corpus c..i shapes the Where wrapper exercises
      "a(b(d(e)),c)", "a(b(c,d,e))", "a(d),b,c(e)",
      "a(c,d),b(e,f)", "a(d),b(e),c(f)", "a(b(d,e,f),c)",
      "a(b(e),c(f),d(g))", "a(b(d,e),c(f,g))", "a(b(d,e),c(f(g)))",
      "a(b(c,d)),e(f(g,h))", "a(d(f,g,h)),b,c(e)",
      "a(b(d,e,f),c(g,h,i))", "a(d(g)),b(e(h)),c(f(i))",
    };

    private static readonly NodeTraversalStrategies[] Strategies =
      Enum
      .GetValues<NodeTraversalStrategies>()
      .Where(strategy => strategy != NodeTraversalStrategies.TraverseAll)
      .ToArray();

    // Each tree is scanned with as many concurrent (node, strategy) skips as fit this many
    // configs. At 35_000 this is a fast "couple of seconds" sweep: full exhaustion for trees
    // up to ~5 nodes and every 3-concurrent-skip combination for larger ones -- enough to
    // catch the bug classes found so far (A/B/C at <=2 skips, D at 3). Raise toward ~1_500_000
    // for a deep occasional sweep (full <=6 nodes, 4-6 concurrent for larger; ~70s) that also
    // covers the 4 concurrent skips the Where wrapper can feed.
    private const long PerTreeBudget = 35_000;

    // Mirrors CombinatorialWhereTests: at the default budget this is only a couple of seconds, so it stays
    // on. Uncomment [Ignore] to skip it in routine runs; raise PerTreeBudget for a deep sweep.
    //[Ignore("Exhaustive core scan -- run occasionally / on core-engine changes.")]
    [TestMethod]
    public void BreadthFirstMatchesDepthFirst()
    {
      long totalConfigs = 0;
      var failureCount = 0;
      var examples = new List<string>();

      foreach (var treeString in TreeStrings)
      {
        var treenumerable =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .Select(visit => visit);

        var nodes =
          treeString
          .Split(new[] { ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

        var maxConcurrent = MaxConcurrentWithinBudget(nodes.Length);

        for (int k = 0; k <= maxConcurrent; k++)
        {
          foreach (var nodeCombination in Combinations(nodes.Length, k))
          {
            foreach (var strategyTuple in StrategyTuples(k))
            {
              totalConfigs++;

              var map = new Dictionary<string, NodeTraversalStrategies>(k);
              for (int i = 0; i < k; i++)
                map[nodes[nodeCombination[i]]] = strategyTuple[i];

              NodeTraversalStrategies Selector(NodeContext<string> nodeContext) =>
                map.TryGetValue(nodeContext.Node, out var strategy)
                ? strategy
                : NodeTraversalStrategies.TraverseAll;

              var breadthFirst = SortedVisits(treenumerable.GetBreadthFirstTraversal(Selector));
              var depthFirst = SortedVisits(treenumerable.GetDepthFirstTraversal(Selector));

              if (!breadthFirst.SequenceEqual(depthFirst))
              {
                failureCount++;
                if (examples.Count < 40)
                {
                  var assignments = string.Join(", ", Enumerable.Range(0, k).Select(i => $"{nodes[nodeCombination[i]]}:{strategyTuple[i]}"));
                  examples.Add($"{treeString} [{assignments}] (BFT {breadthFirst.Length} vs DFT {depthFirst.Length})");
                }
              }
            }
          }
        }
      }

      Assert.AreEqual(
        0,
        failureCount,
        $"BFT diverged from trusted DFT on {failureCount} of {totalConfigs} configs:{Environment.NewLine}"
        + string.Join(Environment.NewLine, examples));
    }

    // Total order over the full visit tuple, so OrderBy + SequenceEqual is a multiset compare.
    // Take(10000) bounds a hypothetical non-terminating regression into a divergence, not a hang.
    private static (TreenumeratorMode Mode, int Depth, int SiblingIndex, int VisitCount, string Node)[] SortedVisits(
      IEnumerable<NodeVisit<string>> visits) =>
      visits
      .Take(10000)
      .Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node))
      .OrderBy(tuple => tuple.Mode)
      .ThenBy(tuple => tuple.Depth)
      .ThenBy(tuple => tuple.SiblingIndex)
      .ThenBy(tuple => tuple.VisitCount)
      .ThenBy(tuple => tuple.Node)
      .ToArray();

    // Largest K such that sum_{k=0..K} C(n,k) * 7^k <= PerTreeBudget (full exhaustion for
    // small n; capped for large n). Never below min(n, 3) so every run covers at least the
    // 3-concurrent-skip space (bug class D) even at a small budget.
    private static int MaxConcurrentWithinBudget(int nodeCount)
    {
      long cumulative = 0;
      long combinations = 1; // C(n, 0)
      var result = 0;

      for (int k = 0; k <= nodeCount; k++)
      {
        var term = combinations * Pow7(k);
        if (cumulative + term > PerTreeBudget && k > Math.Min(nodeCount, 3))
          break;

        cumulative += term;
        result = k;
        combinations = combinations * (nodeCount - k) / (k + 1); // C(n, k+1)
      }

      return result;
    }

    private static long Pow7(int k)
    {
      long result = 1;
      for (int i = 0; i < k; i++)
        result *= 7;
      return result;
    }

    // All size-k index combinations of {0..n-1}.
    private static IEnumerable<int[]> Combinations(int n, int k)
    {
      if (k == 0)
      {
        yield return Array.Empty<int>();
        yield break;
      }

      if (k > n)
        yield break;

      var indices = new int[k];
      for (int i = 0; i < k; i++)
        indices[i] = i;

      while (true)
      {
        yield return indices;

        var pos = k - 1;
        while (pos >= 0 && indices[pos] == n - k + pos)
          pos--;

        if (pos < 0)
          yield break;

        indices[pos]++;
        for (int i = pos + 1; i < k; i++)
          indices[i] = indices[i - 1] + 1;
      }
    }

    // All 7^k strategy tuples (reuses one array; the caller copies values out immediately).
    private static IEnumerable<NodeTraversalStrategies[]> StrategyTuples(int k)
    {
      if (k == 0)
      {
        yield return Array.Empty<NodeTraversalStrategies>();
        yield break;
      }

      var tuple = new NodeTraversalStrategies[k];
      var counter = new int[k];

      while (true)
      {
        for (int i = 0; i < k; i++)
          tuple[i] = Strategies[counter[i]];

        yield return tuple;

        var pos = k - 1;
        while (pos >= 0 && counter[pos] == Strategies.Length - 1)
        {
          counter[pos] = 0;
          pos--;
        }

        if (pos < 0)
          yield break;

        counter[pos]++;
      }
    }
  }
}
