using Copse.Linq;
using Copse.TestUtils;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class PreorderTraversalTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[]
      {
        "a",
        new[] { "a" }
      };
      yield return new object[]
      {
        "a,b,c",
        new[] { "a", "b", "c" }
      };
      yield return new object[]
      {
        "a(b,c)",
        new[] { "a", "b", "c" }
      };
      yield return new object[]
      {
        "a(b(e,f,g),c(h,i,j))",
        new[] { "a", "b", "e", "f", "g", "c", "h", "i", "j" }
      };
      yield return new object[]
      {
        "a(b(c))",
        new[] { "a", "b", "c" }
      };
      yield return new object[]
      {
        "a(b,c),d(e,f)",
        new[] { "a", "b", "c", "d", "e", "f" }
      };
      yield return new object[]
      {
        "a,b(c),d(e(f))",
        new[] { "a", "b", "c", "d", "e", "f" }
      };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void PreorderTraversal(
      string treeString,
      string[] expected)
    {
      // Arrange
      var treenumerable = TreeSerializer.Deserialize(treeString);

      // Act
      var actual =
        treenumerable
        .PreorderTraversal()
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
