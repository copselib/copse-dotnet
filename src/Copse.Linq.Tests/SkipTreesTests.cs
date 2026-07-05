using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class SkipTreesTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      var testData = new[]
      {
        ("a,b,c",         "a,b,c",         0),
        ("a,b,c",         "b,c",           1),
        ("a,b,c",         "c",             2),
        ("a,b,c",         "",              3),
        ("a,b,c",         "",              4),
        ("a(c,d),b(e,f)", "a(c,d),b(e,f)", 0),
        ("a(c,d),b(e,f)", "b(e,f)",        1),
        ("a(c,d),b(e,f)", "",              2),
      };

      foreach (var data in testData)
      {
        yield return new object[] { data.Item1, data.Item2, data.Item3, TreeTraversalStrategy.DepthFirst };
        yield return new object[] { data.Item1, data.Item2, data.Item3, TreeTraversalStrategy.BreadthFirst };
      }
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return $"{data[0]} | {data[1]} | {data[2]} | {((TreeTraversalStrategy)data[3]).ToString().Substring(0, 1)}";
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void SkipTrees(
      string treeString,
      string expectedTreeString,
      int skipCount,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString).SkipTrees(skipCount);

      var expectedTreenumerable = TreeSerializer.DeserializeDepthFirstTree(expectedTreeString);

      Debug.WriteLine("---- Expected Values ----");
      var expected =
        expectedTreenumerable
        .GetTraversal(treeTraversalStrategy)
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      // Act
      Debug.WriteLine("----- Actual Values -----");
      var actual =
        treenumerable
        .GetTraversal(treeTraversalStrategy)
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected.ToArray(), actual.ToArray());
    }
  }
}
