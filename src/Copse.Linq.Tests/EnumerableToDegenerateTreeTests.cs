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
  public class EnumerableToDegenerateTreeTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var testData = new[]
      {
        ""          ,
        "a"         ,
        "a(b)"      ,
        "a(b(c))"   ,
        "a(b(c(d)))",
      };

      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var data in testData)
      {
        yield return new object[]
        {
          data,
          "",
          ""
        };

        var expectedTreeNodes =
          TreeSerializer
          .DeserializeDepthFirstTree(data)
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
            data,
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
              data,
              SerializeTestNodes(firstPair.node, secondPair.node),
              SerializeNodeTraversalStrategies(firstPair.nodeTraversalStrategy, secondPair.nodeTraversalStrategy)
            };
          }
        }
      }
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
      var result =
        data[0].ToString() == ""
        ? "<empty-string>"
        : data[0].ToString();

      if (data[0].ToString() == "")
        return result;

      result += " -> ";

      var testNodes = DeserializeTestNodes(data[1].ToString());
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(data[2].ToString());

      for (int i = 0; i < testNodes.Length; i++)
      {
        if (i == 1)
          result += ", ";

        result += $"{nodeTraversalStrategies[i]}: {testNodes[i]}";
      }

      return result;
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EnumerableToDegenerateTreeTest_BreadthFirst(
      string treeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      EnumerableToDegenerateTreeTest(treeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EnumerableToDegenerateTreeTest_DepthFirst(
      string treeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      EnumerableToDegenerateTreeTest(treeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.DepthFirst);
    }

    public void EnumerableToDegenerateTreeTest(
      string treeString,
      string[] testNodes,
      NodeTraversalStrategies[] nodeTraversalStrategies,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var enumerable = treeString.Where(char.IsLetter).Select(x => x.ToString());

      var sut = enumerable.ToDegenerateTree();

      Func<NodeContext<string>, NodeTraversalStrategies> nodeTraversalStrategiesSelector =
        nodeContext =>
        {
          var testNodeIndex = Array.IndexOf(testNodes, nodeContext.Node);

          if (testNodeIndex == -1)
            return NodeTraversalStrategies.TraverseAll;
          else
            return nodeTraversalStrategies[testNodeIndex];
        };

      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .GetTraversal(
          treeTraversalStrategy,
          nodeTraversalStrategiesSelector)
        .ToArray();

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut
        //.Select(n => $"{n.Left}{n.Right}")
        .GetTraversal(
          treeTraversalStrategy,
          nodeTraversalStrategiesSelector)
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
