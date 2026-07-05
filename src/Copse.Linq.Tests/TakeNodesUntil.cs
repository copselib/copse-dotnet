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
  public class TakeNodesUntilTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var testData = new[]
      {
        new object[] { "",        "",  false, "",        },
        new object[] { "",        "",  true,  "",        },
        new object[] { "a",       "",  false, "a",       },
        new object[] { "a",       "",  true,  "a",       },
        new object[] { "a",       "a", false, "",        },
        new object[] { "a",       "a", true,  "a",       },
        new object[] { "a,b,c",   "",  false, "a,b,c",   },
        new object[] { "a,b,c",   "",  true,  "a,b,c",   },
        new object[] { "a,b,c",   "a", false, "",        },
        new object[] { "a,b,c",   "a", true,  "a",       },
        new object[] { "a,b,c",   "b", false, "a",       },
        new object[] { "a,b,c",   "b", true,  "a,b",     },
        new object[] { "a,b,c",   "c", false, "a,b",     },
        new object[] { "a,b,c",   "c", true,  "a,b,c",   },
        new object[] { "a(b(c))", "",  false, "a(b(c))", },
        new object[] { "a(b(c))", "",  true,  "a(b(c))", },
        new object[] { "a(b(c))", "a", false, "",        },
        new object[] { "a(b(c))", "a", true,  "a",       },
        new object[] { "a(b(c))", "b", false, "a",       },
        new object[] { "a(b(c))", "b", true,  "a(b)",    },
        new object[] { "a(b(c))", "c", false, "a(b)",    },
        new object[] { "a(b(c))", "c", true,  "a(b(c))", },
        new object[] { "a(b,c)",  "",  false, "a(b,c)",  },
        new object[] { "a(b,c)",  "",  true,  "a(b,c)",  },
        new object[] { "a(b,c)",  "a", false, "",        },
        new object[] { "a(b,c)",  "a", true,  "a",       },
        new object[] { "a(b,c)",  "b", false, "a",       },
        new object[] { "a(b,c)",  "b", true,  "a(b)",    },
        new object[] { "a(b,c)",  "c", false, "a(b)",    },
        new object[] { "a(b,c)",  "c", true,  "a(b,c)",  },
      };

      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var data in testData)
      {
        var treeData = data[0].ToString();
        var filter = data[1].ToString();
        var keepNode = (bool)data[2];
        var expectedTreeData = data[3].ToString();

        yield return new object[]
        {
          treeData,
          filter,
          keepNode,
          expectedTreeData,
          "",
          ""
        };

        var expectedTreeNodes =
          TreeSerializer
          .Deserialize(expectedTreeData)
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
              treeData,
              filter,
              keepNode,
              expectedTreeData,
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
              treeData,
              filter,
              keepNode,
              expectedTreeData,
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

      result += $" {data[1]}|{((bool)data[2] ? "T" : "F")} ";

      result += $" -> {data[3]}";

      if (data[4].ToString() == "")
        return result;

      result += " -> ";

      var testNodes = DeserializeTestNodes(data[4].ToString());
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(data[5].ToString());

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
    public void TakeNodesTest_BreadthFirst(
      string treeString,
      string filter,
      bool keepNode,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      TakeNodesTest(treeString, filter, keepNode, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void TakeNodesTest_DepthFirst(
      string treeString,
      string filter,
      bool keepNode,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      TakeNodesTest(treeString, filter, keepNode, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.DepthFirst);
    }

    public void TakeNodesTest(
      string treeString,
      string filter,
      bool keepNode,
      string expectedTreeString,
      string[] testNodes,
      NodeTraversalStrategies[] nodeTraversalStrategies,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var treenumerable = TreeSerializer.Deserialize(treeString);

      var sut =
        treenumerable
        .TakeNodesUntil(nodeContext => nodeContext.Node == filter, keepNode);

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
        .Deserialize(expectedTreeString)
        .GetTraversal(treeTraversalStrategy, nodeTraversalStrategiesSelector)
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
