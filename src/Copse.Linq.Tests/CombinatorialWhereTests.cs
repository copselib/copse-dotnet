using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The exhaustive Where oracle: every tree in the combinatorial corpus x every 0/1/2-node
  // filter combination x composed-vs-single Where spelling x every 0/1/2 (node, consumer
  // strategy) assignment, each case lockstepped against a tree materialized by applying the
  // same filters up front (~891k cases per dimension). Runs as ONE in-process loop per
  // dimension with deserialization caching (the serializer is the slow part): MSTest
  // enumerates [DynamicData] during DISCOVERY -- even for [Ignore]d methods -- and a case set
  // this size overwhelms the host, so per-case DynamicData variants are deliberately absent
  // (the last one was deleted 2026-07-06; this suite is the only exhaustive Where gate).
  [TestClass]
  public class CombinatorialWhereTests
  {
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void BreadthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.BreadthFirst);

    [TestMethod]
    public void DepthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.DepthFirst);

    private void RunScan(TreeTraversalStrategy treeTraversalStrategy)
    {
      var deserializedByString = new Dictionary<string, ITreenumerable<string>>();
      ITreenumerable<string> Deserialize(string treeString)
      {
        if (!deserializedByString.TryGetValue(treeString, out var treenumerable))
        {
          treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);
          deserializedByString[treeString] = treenumerable;
        }
        return treenumerable;
      }

      long total = 0;
      long failed = 0;
      var failures = new List<string>();

      foreach (var data in GenerateCases(CombinatorialTestData.AllTreeStrings))
      {
        total++;

        var treeString = (string)data[0];
        var expectedTreeString = (string)data[1];
        var skippedNodes = (string[])data[2];
        var composeOperations = (bool)data[3];
        var pairs = (CombinatorialTestData.NodeAndTraversalStrategy[])data[4];

        NodeTraversalStrategies Selector(NodeContext<string> nodeContext)
        {
          foreach (var pair in pairs)
            if (pair.Node == nodeContext.Node)
              return pair.NodeTraversalStrategy;
          return NodeTraversalStrategies.TraverseAll;
        }

        ITreenumerable<string> sut;
        if (composeOperations)
        {
          sut = Deserialize(treeString);
          foreach (var node in skippedNodes)
          {
            var skipped = node;
            sut = sut.Where(n => n != skipped);
          }
        }
        else
        {
          sut = Deserialize(treeString).Where(n => !skippedNodes.Contains(n));
        }

        var expected = Key(Deserialize(expectedTreeString).GetTraversal(treeTraversalStrategy, Selector));
        // Take() bounds a hypothetical non-terminating wrapper regression into a length mismatch.
        var actual = Key(sut.GetTraversal(treeTraversalStrategy, Selector)).Take(100_000);

        if (!expected.SequenceEqual(actual))
        {
          failed++;
          if (failures.Count < 40)
            failures.Add(GetCaseDisplayName(data));
        }
      }

      TestContext.WriteLine($"CombinatorialWhereTests ({treeTraversalStrategy}): {total} cases across {CombinatorialTestData.AllTreeStrings.Length} trees (groups c..i).");

      Assert.AreEqual(
        0L,
        failed,
        $"{treeTraversalStrategy} Where wrapper diverged from the oracle on {failed} of {total} cases:{Environment.NewLine}"
        + string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<(TreenumeratorMode, int, int, int, string)> Key(IEnumerable<NodeVisit<string>> visits) =>
      visits.Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node));

    // ---- Case generation ----

    // Exhaustive case generator over a tree set: for each tree, every 0/1/2-node filter
    // combination (with the expected tree computed ONCE per combination by applying the same
    // filters up front), crossed with composed-vs-single Where spelling and every 0/1/2
    // (node, strategy) consumer assignment.
    private static IEnumerable<object[]> GenerateCases(string[] treeStrings)
    {
      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var treeString in treeStrings)
      {
        var allTreeNodes =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .PreorderTraversal()
          .ToArray();

        var allTreeNodeAndTraversalStrategyPairs =
          allTreeNodes
          .SelectMany(node => nodeTraversalStrategies.Select(nodeTraversalStrategy => new CombinatorialTestData.NodeAndTraversalStrategy(node, nodeTraversalStrategy)))
          .ToArray();

        // Combinations of 0, 1, or 2 nodes that will not satisfy the where clause.
        var treeNodeCombinations = GetTreeNodeCombinationsUpToCount(treeString, 2).ToArray();

        // Combinations of 0, 1, or 2 node / node traversal strategy pairs.
        var treeNodeAndTraversalStrategyCombinations = Combinatorics.GetCombinationsUpToCount<CombinatorialTestData.NodeAndTraversalStrategy>(allTreeNodeAndTraversalStrategyPairs.AsSpan(), 2).ToArray();

        foreach (var nodeCombinations in treeNodeCombinations)
        {
          var expectedTreeString = GetExpectedTreeString(treeString, nodeCombinations);

          foreach (var composeOperations in new[] { true, false })
          {
            foreach (var nodeAndTraversalStrategyPairCombination in treeNodeAndTraversalStrategyCombinations)
            {
              var nodeAndTraversalStrategyPairs = nodeAndTraversalStrategyPairCombination.ToArray();

              yield return new object[]
              {
                treeString,
                expectedTreeString,
                nodeCombinations,
                composeOperations,
                nodeAndTraversalStrategyPairs
              };
            }
          }
        }
      }
    }

    private static IEnumerable<string[]> GetTreeNodeCombinationsUpToCount(string treeString, int count)
    {
      var nodes =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .PreorderTraversal()
        .ToArray()
        .AsSpan();

      return
        Combinatorics
        .GetCombinationsUpToCount<string>(nodes, count)
        .Select(combination => combination.Select(node => node.ToString()).ToArray());
    }

    private static string GetExpectedTreeString(string treeString, IEnumerable<string> whereNotNodes)
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expectedTree = tree;

      foreach (var node in whereNotNodes)
        expectedTree = expectedTree.Where(n => n != node).Hide();

      return TreeSerializer.SerializeDepthFirstTree(expectedTree);
    }

    private static string GetCaseDisplayName(object[] data)
    {
      var result = $"{data[0]} -> {data[1]} ";

      var nodeAndTraversalStrategyPairs = (CombinatorialTestData.NodeAndTraversalStrategy[])data[4];

      for (int i = 0; i < nodeAndTraversalStrategyPairs.Length; i++)
      {
        if (i > 0)
          result += ", ";

        result += $"{nodeAndTraversalStrategyPairs[i].Node}: {nodeAndTraversalStrategyPairs[i].NodeTraversalStrategy}";
      }

      result += $" ({data[3].ToString().Substring(0, 1)})";

      return result;
    }
  }
}
