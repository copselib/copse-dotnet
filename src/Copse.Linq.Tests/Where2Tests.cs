using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  //[Ignore("This generates a ton of tests and takes forever. I only run it occasionally.")]
  public class Where2Tests
  {
    public struct NodeAndTraversalStrategy
    {
      public NodeAndTraversalStrategy(string node, NodeTraversalStrategies nodeTraversalStrategy)
      {
        if (node == null)
          throw new ArgumentNullException(nameof(node));

        Node = node;
        NodeTraversalStrategy = nodeTraversalStrategy;
      }

      public string Node { get; set; }
      public NodeTraversalStrategies NodeTraversalStrategy { get; set; }
    }

    // Full exhaustive tree set (groups c..i, 3..9 nodes). Consumed by the in-process
    // Where2InProcessScan, which loops these directly -- no per-case MSTest discovery.
    // Do NOT feed this to the [DynamicData] methods below: MSTest enumerates DynamicData
    // during discovery even for [Ignore]d tests, and this many cases overwhelms the host.
    public static readonly string[] AllTreeStrings =
    {
      // c
      "a(b(c))",
      "a(b,c)",
      "a,b(c)",
      "a,b,c",

      // d
      "a(b,c,d)",
      "a,b(c,d)",
      "a,b(d),c",

      // e
      "a(b(d(e)),c)",
      "a(b(c,d,e))",
      "a(d),b,c(e)",

      // f
      "a(c,d),b(e,f)",
      "a(d),b(e),c(f)",
      "a(b(d,e,f),c)",
      "a,b(d),c(e(f))",

      // g
      "a(b(e),c(f),d(g))",
      "a(b(d,e),c(f(g)))",

      // h
      "a(d(f,g,h)),b,c(e)",

      // i
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
    };

    // Small subset for the [DynamicData] methods only. Kept tiny so MSTest discovery stays
    // cheap (those methods are [Ignore]d and superseded by Where2InProcessScan anyway).
    private static readonly string[] DynamicDataTreeStrings =
    {
      "a(b(c))",
      "a(b,c)",
      "a,b(c)",
      "a,b,c",
    };

    // DynamicData source for the (ignored) per-case methods -- intentionally the small set.
    public static IEnumerable<object[]> GetTestData() => GenerateCases(DynamicDataTreeStrings);

    // Exhaustive case generator over an arbitrary tree set. The in-process scan calls this
    // with AllTreeStrings; GetTestData calls it with the small DynamicData set.
    public static IEnumerable<object[]> GenerateCases(string[] treeStrings)
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
          .SelectMany(node => nodeTraversalStrategies.Select(nodeTraversalStrategy => new NodeAndTraversalStrategy(node, nodeTraversalStrategy)))
          .ToArray();

        // Get combinations of 0, 1, or 2 nodes that will not satisfy where clause in tests.
        var treeNodeCombinations = GetTreeNodeCombinationsUpToCount(treeString, 2).ToArray();

        // Get combinations of 0, 1, or 2 node / node traversal strategy pairs for use in the tests.
        var treeNodeAndTraversalStrategyCombinations = Combinatorics.GetCombinationsUpToCount<NodeAndTraversalStrategy>(allTreeNodeAndTraversalStrategyPairs.AsSpan(), 2).ToArray();

        foreach (var nodeCombinations in treeNodeCombinations)
        {
          // expectedTreeString depends only on (treeString, nodeCombinations), so compute it
          // once per filter combo instead of redundantly inside the compose/strategy loops
          // (was ~118k Deserialize/Where/Serialize passes -> 37 distinct results for the 8-node
          // h-tree). Behavior-preserving cleanup only -- it is NOT the lever for big-tree run
          // time, which is dominated by MSTest's per-case overhead for the ~118k+ DynamicData
          // cases, not this computation.
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
        expectedTree = expectedTree.Where(nc => nc.Node != node).Hide();

      return TreeSerializer.SerializeDepthFirstTree(expectedTree);
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      var result = $"{data[0]} -> {data[1]} ";

      var nodeAndTraversalStrategyPairs = (NodeAndTraversalStrategy[])data[4];

      for (int i = 0; i < nodeAndTraversalStrategyPairs.Length; i++)
      {
        if (i > 0)
          result += ", ";

        result += $"{nodeAndTraversalStrategyPairs[i].Node}: {nodeAndTraversalStrategyPairs[i].NodeTraversalStrategy}";
      }

      result += $" ({data[3].ToString().Substring(0, 1)})";

      return result;
    }

    // The Where2Test_* methods below are [DynamicData] test methods. MSTest ENUMERATES [DynamicData] at
    // DISCOVERY time -- on every `dotnet test` of this assembly -- EVEN for [Ignore]d methods, and that
    // enumeration runs GetTestData()/GenerateCases() in full (deserialize + Where + Serialize per case).
    // That combinatorial discovery dominated test-run time. They are superseded by the fast in-process
    // Where2InProcessScan (identical coverage in seconds), so they are excluded from compilation entirely
    // via `#if false` and are therefore never discovered. The shared DATA/helpers ABOVE (AllTreeStrings,
    // GenerateCases, NodeAndTraversalStrategy, GetTestData, GetTestDisplayName) stay compiled because the
    // in-process scans and MergeInProcessScan depend on them. To run the slow exhaustive per-case variant,
    // flip `#if false` to `#if true`.
#if false
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    [Ignore("Superseded by the fast in-process Where2InProcessScan (same BFT-vs-oracle coverage in ~seconds). Un-ignore for the slow exhaustive per-case DynamicData variant.")]
    public void Where2Test_BreadthFirst(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      bool composeOperations,
      NodeAndTraversalStrategy[] nodeAndTraversalStrategyPairs)
    {
      Where2Test(
        treeString,
        expectedTreeString,
        skippedNodes,
        composeOperations,
        nodeAndTraversalStrategyPairs,
        TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    [Ignore("Too slow to discover with many trees enabled; DFT is the trusted oracle and is covered by the curated WhereTests. Un-ignore for the slow exhaustive DFT-vs-oracle DynamicData run.")]
    public void Where2Test_DepthFirst(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      bool composeOperations,
      NodeAndTraversalStrategy[] nodeAndTraversalStrategyPairs)
    {
      Where2Test(
        treeString,
        expectedTreeString,
        skippedNodes,
        composeOperations,
        nodeAndTraversalStrategyPairs,
        TreeTraversalStrategy.DepthFirst);
    }

    public void Where2Test(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      bool composeOperations,
      NodeAndTraversalStrategy[] nodeAndTraversalStrategyPairs,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      if (nodeAndTraversalStrategyPairs.Any(x => x.Node == null || x.NodeTraversalStrategy == NodeTraversalStrategies.TraverseAll))
        throw new InternalTestFailureException();

      ITreenumerable<string> sut;

      if (composeOperations)
      {
        sut = treenumerable;

        foreach (var node in skippedNodes)
          sut = sut.Where(nc => nc.Node != node);
      }
      else
      {
        sut =
          treenumerable
          .Where(nc => !skippedNodes.Contains(nc.Node));
      }

      Func<NodeContext<string>, NodeTraversalStrategies> nodeTraversalStrategySelector =
        nodeContext =>
        {
          var testNodeIndex = -1;

          for (int i = 0; i < nodeAndTraversalStrategyPairs.Length; i++)
          {
            if (nodeAndTraversalStrategyPairs[i].Node == nodeContext.Node)
            {
              testNodeIndex = i;
              break;
            }
          }

          if (testNodeIndex == -1)
            return NodeTraversalStrategies.TraverseAll;
          else
            return nodeAndTraversalStrategyPairs[testNodeIndex].NodeTraversalStrategy;
        };

      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(treeTraversalStrategy, nodeTraversalStrategySelector)
        .ToArray();

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut
        .GetTraversal(
          treeTraversalStrategy,
          nodeTraversalStrategySelector)
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      var diff = NodeVisitDiffer.Diff(expected, actual);

      Debug.WriteLine($"{Environment.NewLine}-----Diffed Values-----");
      foreach (var diffResult in diff)
        Debug.WriteLine(diffResult);

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }
#endif
  }
}
