using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class PostorderTraversalTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[] { "", Array.Empty<string>() };
      yield return new object[] { "a", new[] { "a" } };
      yield return new object[] { "a(c),b", new[] { "c", "a", "b" } };
      yield return new object[] { "a(b(c))", new[] { "c", "b", "a" } };
      yield return new object[] { "a(b,c)", new[] { "b", "c", "a" } };
      yield return new object[] { "a(c,d),b(e,f)", new[] { "c", "d", "a", "e", "f", "b" } };
      yield return new object[] { "a,b(c)", new[] { "a", "c", "b" } };
      yield return new object[] { "a(d(f)),b(e),c", new[] { "f", "d", "a", "e", "b", "c" } };
      yield return new object[] { "a,b(d),c(e(f))", new[] { "a", "d", "b", "f", "e", "c" } };
      yield return new object[] { "a,b,c", new[] { "a", "b", "c" } };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void PostorderTraversal(
      string treeString,
      string[] expectedResults)
    {
      // Arrange
      var treenumerable = TreeSerializer.Deserialize(treeString);

      // Act
      var actual =
        treenumerable
        .PostorderTraversal()
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expectedResults, actual);
    }
  }
}
