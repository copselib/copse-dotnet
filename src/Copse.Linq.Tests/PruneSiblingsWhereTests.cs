using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  // PruneSiblingsWhere's contract IS a strategy: the operator must produce exactly the
  // visit stream of the raw source driven with a consumer PruneSiblings at every matched
  // node -- and, under consumer strategies of its own, the OR of the two. That equivalence
  // is the oracle for every test here; the engine's native PruneSiblings handling is
  // conformance-pinned separately.
  [TestClass]
  public class PruneSiblingsWhereTests
  {
    private static readonly string[] TreeStrings = new[]
    {
      "a",
      "a,b,c",
      "a(b,c)",
      "a(b(c))",
      "a,b(d),c",
      "a(b,c),d(e,f)",
      "a(b(e,f,g),c)",
      "a,b(c),d(e(f))",
      "a(b(e,f,g),c(h,i,j))",
    };

    public static IEnumerable<object[]> GetNodeCases()
    {
      foreach (var treeString in TreeStrings)
        foreach (var node in TreeSerializer.DeserializeDepthFirstTree(treeString).GetPreorderTraversal())
          foreach (var treeTraversalStrategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
            yield return new object[] { treeString, node, treeTraversalStrategy };
    }

    public static IEnumerable<object[]> GetConsumerStrategyCases()
    {
      var consumerStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(strategies => strategies != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var treeString in new[] { "a(b,c),d(e,f)", "a(b(e,f,g),c(h,i,j))" })
      {
        var nodes = TreeSerializer.DeserializeDepthFirstTree(treeString).GetPreorderTraversal().ToArray();

        foreach (var prunedNode in nodes)
          foreach (var consumerNode in nodes)
            foreach (var consumerStrategy in consumerStrategies)
              foreach (var treeTraversalStrategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
                yield return new object[] { treeString, prunedNode, consumerNode, consumerStrategy, treeTraversalStrategy };
      }
    }

    public static string GetNodeCaseDisplayName(MethodInfo methodInfo, object[] data)
      => $"{data[0]} prune-siblings-at:{data[1]} {data[2]}";

    public static string GetConsumerStrategyCaseDisplayName(MethodInfo methodInfo, object[] data)
      => $"{data[0]} prune-siblings-at:{data[1]} consumer:{data[3]}@{data[2]} {data[4]}";

    private static NodeVisit<string>[] Drive(
      ITreenumerable<string> treenumerable,
      TreeTraversalStrategy treeTraversalStrategy,
      Func<string, NodeTraversalStrategies> nodeTraversalStrategySelector)
      => treenumerable.GetTraversal(treeTraversalStrategy, nodeTraversalStrategySelector).ToArray();

    [TestMethod]
    [DynamicData(nameof(GetNodeCases), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetNodeCaseDisplayName))]
    public void Matches_the_consumer_strategy_oracle(string treeString, string prunedNode, TreeTraversalStrategy treeTraversalStrategy)
    {
      var source = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expected = Drive(
        source,
        treeTraversalStrategy,
        node => node == prunedNode ? NodeTraversalStrategies.PruneSiblings : NodeTraversalStrategies.TraverseAll);

      var actual = Drive(
        source.PruneSiblingsWhere(node => node == prunedNode),
        treeTraversalStrategy,
        node => NodeTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(expected, actual, string.Join(" ", NodeVisitDiffer.Diff(expected, actual)));
    }

    [TestMethod]
    [DynamicData(nameof(GetNodeCases), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetNodeCaseDisplayName))]
    public void Positional_predicate_matches_the_oracle(string treeString, string prunedNode, TreeTraversalStrategy treeTraversalStrategy)
    {
      var source = TreeSerializer.DeserializeDepthFirstTree(treeString);

      // The corpus nodes are unique, so the scheduled position keyed by node stands in for
      // "the predicate saw (node, this layer's input labels)".
      var positions = Drive(source, treeTraversalStrategy, node => NodeTraversalStrategies.TraverseAll)
        .Where(visit => visit.VisitCount == 1)
        .GroupBy(visit => visit.Node)
        .ToDictionary(group => group.Key, group => group.First().Position);

      var expected = Drive(
        source,
        treeTraversalStrategy,
        node => node == prunedNode ? NodeTraversalStrategies.PruneSiblings : NodeTraversalStrategies.TraverseAll);

      var actual = Drive(
        source.PruneSiblingsWhere((node, position) => node == prunedNode && position == positions[prunedNode]),
        treeTraversalStrategy,
        node => NodeTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(expected, actual, string.Join(" ", NodeVisitDiffer.Diff(expected, actual)));
    }

    [TestMethod]
    [DynamicData(nameof(GetNodeCases), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetNodeCaseDisplayName))]
    public void Composes_over_a_select_chain(string treeString, string prunedNode, TreeTraversalStrategy treeTraversalStrategy)
    {
      var source = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expected = Drive(
        source,
        treeTraversalStrategy,
        node => node == prunedNode ? NodeTraversalStrategies.PruneSiblings : NodeTraversalStrategies.TraverseAll);

      // Select first, so the operator arrives on a composition citizen and takes the splice path.
      var actual = Drive(
        source.Select(node => node).PruneSiblingsWhere(node => node == prunedNode),
        treeTraversalStrategy,
        node => NodeTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(expected, actual, string.Join(" ", NodeVisitDiffer.Diff(expected, actual)));
    }

    [TestMethod]
    [DynamicData(nameof(GetConsumerStrategyCases), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetConsumerStrategyCaseDisplayName))]
    public void Consumer_strategies_compose_by_union(
      string treeString,
      string prunedNode,
      string consumerNode,
      NodeTraversalStrategies consumerStrategy,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      var source = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expected = Drive(
        source,
        treeTraversalStrategy,
        node =>
          (node == consumerNode ? consumerStrategy : NodeTraversalStrategies.TraverseAll)
          | (node == prunedNode ? NodeTraversalStrategies.PruneSiblings : NodeTraversalStrategies.TraverseAll));

      var actual = Drive(
        source.PruneSiblingsWhere(node => node == prunedNode),
        treeTraversalStrategy,
        node => node == consumerNode ? consumerStrategy : NodeTraversalStrategies.TraverseAll);

      CollectionAssert.AreEqual(expected, actual, string.Join(" ", NodeVisitDiffer.Diff(expected, actual)));
    }
  }
}
