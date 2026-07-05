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
  public class TreeSerializerTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      for (int i = 0; i < TreeTraversalTestData.TestTrees.Length; i++)
      {
        var testTree = TreeTraversalTestData.TestTrees[i];

        var testScenarioFound = false;

        for (int j = 0; j < testTree.TestScenarios.Count; j++)
        {
          var testScenario = testTree.TestScenarios[j];

          if (testScenario.Description == "Traverse all")
          {
            testScenarioFound = true;
            yield return new object[] { testTree.TreeString, testScenario.Description, i, j };
          }
        }

        if (!testScenarioFound)
          throw new InternalTestFailureException($"Test scenario not found for '{testTree.TreeString}'.");
      }
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return $"{data[0]} -> {data[1]}";
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void Deserialize(
      string treeString,
      string testDescription,
      int testTreeIndex,
      int testScenarioIndex)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);
      var testScenario = TreeTraversalTestData.TestTrees[testTreeIndex].TestScenarios[testScenarioIndex];

      var expected = testScenario.ExpectedDepthFirstResults;

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        treenumerable
        .GetDepthFirstTraversal()
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void Serialize(
      string treeString,
      string testDescription,
      int testTreeIndex,
      int testScenarioIndex)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      // Act
      var serializedTreeString = treenumerable.SerializeDepthFirstTree();

      // Assert
      Assert.AreEqual(treeString, serializedTreeString);
    }
  }
}
