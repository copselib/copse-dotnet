using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Copse.Tests
{
  [TestClass]
  public class TraversalTests
  {
    static TraversalTests()
    {
      _TreenumerableTestDataFactory = new TreenumerableTestDataFactory(TreeTraversalTestData.TestTrees);
    }

    private static TreenumerableTestDataFactory _TreenumerableTestDataFactory;

    public static IEnumerable<object[]> GetTestData()
      => _TreenumerableTestDataFactory.GetTestData();

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
      => _TreenumerableTestDataFactory.GetTestDisplayName(methodInfo, data);

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void DepthFirstTraversal(
      string treeString,
      string testDescription,
      int testTreeIndex,
      int testScenarioIndex)
    {
      TraversalTest(treeString, testTreeIndex, testScenarioIndex, true);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void BreadthFirstTraversal(
      string treeString,
      string testDescription,
      int testTreeIndex,
      int testScenarioIndex)
    {
      TraversalTest(treeString, testTreeIndex, testScenarioIndex, false);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void DepthFirstVsBreadthFirstValues(
      string treeString,
      string testDescription,
      int testTreeIndex,
      int testScenarioIndex)
    {
      // Arrange
      var treenumerable =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .Select(visit => visit);

      var testScenario = TreeTraversalTestData.TestTrees[testTreeIndex].TestScenarios[testScenarioIndex];

      NodeVisit<string>[] Sort(IEnumerable<NodeVisit<string>> nodes) =>
        nodes
        .OrderBy(x => (x.Mode, x.Position.Depth, x.Position.SiblingIndex, x.Node))
        .ToArray();

      // Act
      Debug.WriteLine("-----Breadth First-----");
      var breadthFirst =
        treenumerable
        .GetBreadthFirstTraversal(testScenario.NodeTraversalStrategiesSelector)
        .Do(x => Debug.WriteLine(x))
        .ToArray();

      Debug.WriteLine($"{Environment.NewLine}-----Depth First------");
      var depthFirst =
        treenumerable
        .GetDepthFirstTraversal(testScenario.NodeTraversalStrategiesSelector)
        .Do(x => Debug.WriteLine(x))
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(Sort(breadthFirst), Sort(depthFirst));
    }

    private void TraversalTest(
      string treeString,
      int testTreeIndex,
      int testScenarioIndex,
      bool depthFirstTest)
    {
      // Arrange
      var testScenario = TreeTraversalTestData.TestTrees[testTreeIndex].TestScenarios[testScenarioIndex];

      ITreenumerable<string> treenumerable;

      treenumerable =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .Select(visit => visit);

      var expected =
        depthFirstTest
        ? testScenario.ExpectedDepthFirstResults
        : testScenario.ExpectedBreadthFirstResults;

      Debug.WriteLine("--------- Tree ---------");
      Debug.WriteLine(treeString);

      Debug.WriteLine($"{Environment.NewLine}---- Expected Values ----");
      NodeVisitsDebugWriter.WriteNodeVisitHeader();
      foreach (var nodeVisit in expected)
        Debug.WriteLine(nodeVisit);

      var moveNextEnumerable =
        depthFirstTest
        ? treenumerable.GetDepthFirstTraversal(testScenario.NodeTraversalStrategiesSelector)
        : treenumerable.GetBreadthFirstTraversal(testScenario.NodeTraversalStrategiesSelector);

      // Act
      Debug.WriteLine($"{Environment.NewLine}----- Actual Values -----");
      NodeVisitsDebugWriter.WriteNodeVisitHeader();
      var actual =
        moveNextEnumerable
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
