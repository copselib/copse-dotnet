using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class LevelOrderTraversalTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[] { "", Array.Empty<string>() };
      yield return new object[] { "a", new[] { "a" } };
      yield return new object[] { "a(c),b", new[] { "a", "b", "c" } };
      yield return new object[] { "a(b(c))", new[] { "a", "b", "c" } };
      yield return new object[] { "a(b,c)", new[] { "a", "b", "c" } };
      yield return new object[] { "a(c,d),b(e,f)", new[] { "a", "b", "c", "d", "e", "f" } };
      yield return new object[] { "a,b(c)", new[] { "a", "b", "c" } };
      yield return new object[] { "a(d(f)),b(e),c", new[] { "a", "b", "c", "d", "e", "f" } };
      yield return new object[] { "a,b(d),c(e(f))", new[] { "a", "b", "c", "d", "e", "f" } };
      yield return new object[] { "a,b,c", new[] { "a", "b", "c" } };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void LevelOrderTraversal(
      string treeString,
      string[] expectedResults)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      // Act
      var actual =
        treenumerable
        .LevelOrderTraversal()
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expectedResults, actual);
    }
  }
}
