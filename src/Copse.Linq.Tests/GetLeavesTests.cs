using Copse.Linq;
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
  public class GetLeavesTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[] { "a", new[] { "a" } };
      yield return new object[] { "a(b),c", new[] { "b", "c" } };
      yield return new object[] { "a(b(c))", new[] { "c" } };
      yield return new object[] { "a(b,c)", new[] { "b", "c" } };
      yield return new object[] { "a(b,c),d(e,f)", new[] { "b", "c", "e", "f" } };
      yield return new object[] { "a,b(c)", new[] { "a", "c" } };
      yield return new object[] { "a,b(c),d(e(f))", new[] { "a", "c", "f" } };
      yield return new object[] { "a,b,c", new[] { "a", "b", "c" } };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void GetLeaves(
      string treeString,
      string[] expected)
    {
      // Arrange
      var treenumerable = TreeSerializer.Deserialize(treeString);

      // Act
      var actual =
        treenumerable
        .GetLeaves()
        .Do(node => Debug.WriteLine(node))
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
