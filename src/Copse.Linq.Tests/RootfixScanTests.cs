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
  public class RootfixScanTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var testData = new[]
      {
        new[] { "",                     ""                                   },
        new[] { "a",                    "a"                                  },
        new[] { "a,b,c",                "a,b,c"                              },
        new[] { "a(b,c)",               "a(ab,ac)"                           },
        new[] { "a,b(c)",               "a,b(bc)"                            },
        new[] { "a(b(e,f,g),c(h,i,j))", "a(ab(abe,abf,abg),ac(ach,aci,acj))" },
        new[] { "a(b(c))",              "a(ab(abc))"                         },
        new[] { "a(b,c),d(e,f)",        "a(ab,ac),d(de,df)"                  },
        new[] { "a,b(c),d(e(f))",       "a,b(bc),d(de(def))"                 },
      };

      var nodeTraversalStrategies =
        Enum.GetValues(typeof(NodeTraversalStrategies))
        .Cast<NodeTraversalStrategies>()
        .Where(nodeTraversalStrategy => nodeTraversalStrategy != NodeTraversalStrategies.TraverseAll)
        .ToArray();

      foreach (var data in testData)
      {
        var treeData = data[0];
        var expectedTreeData = data[1];

        yield return new object[]
        {
          treeData,
          expectedTreeData,
          "",
          ""
        };

        var expectedTreeNodes =
          TreeSerializer
          .DeserializeDepthFirstTree(expectedTreeData)
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

      if (data[2].ToString() == "")
        return result;

      result += " -> ";

      var testNodes = DeserializeTestNodes(data[2].ToString());
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(data[3].ToString());

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
    public void RootfixScanTest_BreadthFirst(
      string treeString,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      RootfixScanTest(treeString, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void RootfixScanTest_DepthFirst(
      string treeString,
      string expectedTreeString,
      string testNodesString,
      string nodeTraversalStrategiesString)
    {
      var testNodes = DeserializeTestNodes(testNodesString);
      var nodeTraversalStrategies = DeserializeNodeTraversalStrategies(nodeTraversalStrategiesString);

      RootfixScanTest(treeString, expectedTreeString, testNodes, nodeTraversalStrategies, TreeTraversalStrategy.DepthFirst);
    }

    public void RootfixScanTest(
      string treeString,
      string expectedTreeString,
      string[] testNodes,
      NodeTraversalStrategies[] nodeTraversalStrategies,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var sut =
        treenumerable
        .RootfixScan((accumulate, visit) => accumulate.Node + visit.Node, "");

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
