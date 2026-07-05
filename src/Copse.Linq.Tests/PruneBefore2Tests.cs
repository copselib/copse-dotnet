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
  public class PruneBefore2Tests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var treeStrings = new[]
      {
        "a,b,c",
        //"a(b,c)",
        //"a,b(c)",
        //"a(b(c))",
        //"a,b(d),c",
        //"a(b,c),d(e,f)",
        //"a(b(e,f,g),c)",
        //"a,b(c),d(e(f))",
        //"a(b(e,f,g),c(h,i,j))",
      };

      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var treeString in treeStrings)
      {
        var nodePairs = GetTreeNodePairs(treeString).ToArray();

        foreach (var nodePair in nodePairs)
        {
          var expectedTreeString = GetExpectedTreeString(treeString, nodePair[0], nodePair[1]);

          yield return new object[]
          {
            treeString,
            expectedTreeString,
            nodePair,
            "",
            ""
          };

          var expectedTreeNodes =
            TreeSerializer
            .DeserializeDepthFirstTree(treeString)
            .PreorderTraversal()
            .ToArray();

          var cross =
            expectedTreeNodes
            .SelectMany(node => nodeTraversalStrategies.Select(nodeTraversalStrategy => (node, nodeTraversalStrategy)))
            .ToArray();

          for (int i = 0; i < cross.Length; i++)
          {
            var firstPair = cross[i];

            yield return new object[]
            {
              treeString,
              expectedTreeString,
              nodePair,
              firstPair.node,
              firstPair.nodeTraversalStrategy.ToString()
            };

            for (int j = i + 1; j < cross.Length; j++)
            {
              var secondPair = cross[j];

              if (firstPair.node == secondPair.node)
                continue;

              yield return new object[]
              {
                treeString,
                expectedTreeString,
                nodePair,
                SerializeTestNodes(firstPair.node, secondPair.node),
                SerializeNodeTraversalStrategies(firstPair.nodeTraversalStrategy, secondPair.nodeTraversalStrategy)
              };
            }
          }
        }
      }
    }

    private static IEnumerable<string[]> GetTreeNodePairs(string treeString)
    {
      var nodes = TreeSerializer.DeserializeDepthFirstTree(treeString).PreorderTraversal().ToArray();

      for (int i = 0; i < nodes.Length - 1; i++)
        for (int j = i + 1; j < nodes.Length; j++)
          yield return new[] { nodes[i], nodes[j] };
    }

    private static string GetExpectedTreeString(string treeString, string node1, string node2)
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expectedTree =
        tree
        .PruneBefore(nc => nc.Node == node1)
        .Hide()
        .PruneBefore(nc => nc.Node == node2);

      return TreeSerializer.SerializeDepthFirstTree(expectedTree);
    }

    private static string SerializeNodeTraversalStrategies(params NodeTraversalStrategies[] nodeTraversalStrategies)
    {
      return string.Join("|", nodeTraversalStrategies.Select(nodeTraversalStrategy => nodeTraversalStrategy.ToString()));
    }

    private static NodeTraversalStrategies[] DeserializeNodeTraversalStrategies(string nodeTraversalStrategies)
    {
      if (string.IsNullOrEmpty(nodeTraversalStrategies))
        return Array.Empty<NodeTraversalStrategies>();

      return
        nodeTraversalStrategies
        .Split('|')
        .Select(nodeTraversalStrategy => Enum.Parse(typeof(NodeTraversalStrategies), nodeTraversalStrategy))
        .Cast<NodeTraversalStrategies>()
        .ToArray();
    }

    private static string SerializeTestNodes(params string[] testNodes)
    {
      return string.Join("|", testNodes);
    }

    private static string[] DeserializeTestNodes(string testNodes)
    {
      if (string.IsNullOrEmpty(testNodes))
        return Array.Empty<string>();

      return testNodes.Split('|');
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      var result = $"{data[0]} -> {data[1]} ";

      result += $"PB:{string.Join("", data[2] as object[])} ";

      var testNodes = DeserializeTestNodes(data[3].ToString());
      var nodeTraversalStrategy = DeserializeNodeTraversalStrategies(data[4].ToString());

      for (int i = 0; i < testNodes.Length; i++)
      {
        if (i == 1)
          result += ", ";

        result += $"{nodeTraversalStrategy[i]}: {testNodes[i]}";
      }

      return result;
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void PruneBefore2Test_BreadthFirst(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      string testNodesString,
      string nodeTraversalStrategyString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategy = DeserializeNodeTraversalStrategies(nodeTraversalStrategyString);

      PruneBefore2Test(
        treeString,
        expectedTreeString,
        skippedNodes,
        testNodes,
        nodeTraversalStrategy,
        TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void PruneBefore2Test_DepthFirst(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      string testNodesString,
      string nodeTraversalStrategyString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategy = DeserializeNodeTraversalStrategies(nodeTraversalStrategyString);

      PruneBefore2Test(
        treeString,
        expectedTreeString,
        skippedNodes,
        testNodes,
        nodeTraversalStrategy,
        TreeTraversalStrategy.DepthFirst);
    }

    public void PruneBefore2Test(
      string treeString,
      string expectedTreeString,
      string[] skippedNodes,
      string[] testNodes,
      NodeTraversalStrategies[] nodeTraversalStrategy,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var sut =
        treenumerable
        .PruneBefore(nc => nc.Node == skippedNodes[0])
        .PruneBefore(nc => nc.Node == skippedNodes[1]);

      Func<NodeContext<string>, NodeTraversalStrategies> nodeTraversalStrategySelector =
        nodeContext =>
        {
          var testNodeIndex = Array.IndexOf(testNodes, nodeContext.Node);

          if (testNodeIndex == -1)
            return NodeTraversalStrategies.TraverseAll;
          else
            return nodeTraversalStrategy[testNodeIndex];
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
  }
}
