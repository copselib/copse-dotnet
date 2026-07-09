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
  public class UnionTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var testData = new[]
      {
        new[] { "",            "",            ""                     },
        new[] { "a",           "0",           "a0"                   },
        new[] { "a",           "0,1",         "a0,1"                 },
        new[] { "a,b",         "0",           "a0,b"                 },
        new[] { "a",           "0(1)",        "a0(1)"                },
        new[] { "a(b)",        "0",           "a0(b)"                },
        new[] { "a(b)",        "0,1",         "a0(b),1"              },
        new[] { "a,b",         "0(1)",        "a0(1),b"              },
        new[] { "a,b(c)",      "0(1)",        "a0(1),b(c)"           },
        new[] { "a(b,c),d",    "0,1(2,3)",    "a0(b,c),d1(2,3)"      },
        new[] { "a,b(c,d)",    "0(2,3),1",    "a0(2,3),b1(c,d)"      },
        new[] { "a(b(c(d)))",  "0(1(2(3)))",  "a0(b1(c2(d3)))"       },
        new[] { "a,b,c,d",     "0,1,2,3",     "a0,b1,c2,d3"          },
        new[] { "a(b(c))",     "0(1)",        "a0(b1(c))"            },
        new[] { "a(d(e)),b,c", "0,1,2(3(4))", "a0(d(e)),b1,c2(3(4))" },
      };

      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var data in testData)
      {
        var leftTreeData = data[0];
        var rightTreeData = data[1];
        var joinedTreeData = data[2];

        yield return new object[]
        {
          leftTreeData,
          rightTreeData,
          joinedTreeData,
          "",
          ""
        };

        var expectedTreeNodes =
          TreeSerializer
          .DeserializeDepthFirstTree(joinedTreeData)
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
            leftTreeData,
            rightTreeData,
            joinedTreeData,
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
              leftTreeData,
              rightTreeData,
              joinedTreeData,
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
        data[2].ToString() == ""
        ? "<empty-string>"
        : data[2].ToString();

      if (data[2].ToString() == "")
        return result;

      result += " -> ";

      var testNodes = DeserializeTestNodes(data[3].ToString());
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(data[4].ToString());

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
    public void UnionTest_BreadthFirst(
      string leftTreeString,
      string rightTreeString,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      UnionTest(leftTreeString, rightTreeString, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void UnionTest_DepthFirst(
      string leftTreeString,
      string rightTreeString,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      UnionTest(leftTreeString, rightTreeString, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.DepthFirst);
    }

    public void UnionTest(
      string leftTreeString,
      string rightTreeString,
      string expectedTreeString,
      string[] testNodes,
      NodeTraversalStrategies[] nodeTraversalStrategies,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var leftTreenumerable = TreeSerializer.DeserializeDepthFirstTree(leftTreeString);
      var rightTreenumerable = TreeSerializer.DeserializeDepthFirstTree(rightTreeString);

      var sut = leftTreenumerable.Union(rightTreenumerable);

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
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(
          treeTraversalStrategy,
          nodeTraversalStrategiesSelector)
        .ToArray();

      Debug.WriteLine($"Left Tree: {leftTreeString}\r\n");
      Debug.WriteLine($"Right Tree: {rightTreeString}\r\n");

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut
        .Select(nodeContext => $"{nodeContext.Node.Left}{nodeContext.Node.Right}")
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
