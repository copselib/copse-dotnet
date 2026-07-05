using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class GetRootsTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[] { "a", new[] { "a" } };
      yield return new object[] { "a,b,c", new[] { "a", "b", "c" } };
      yield return new object[] { "a(b,c)", new[] { "a" } };
      yield return new object[] { "a(b(c))", new[] { "a" } };
      yield return new object[] { "a(b,c),d(e,f)", new[] { "a", "d" } };
      yield return new object[] { "a,b(c),d(e(f))", new[] { "a", "b", "d" } };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void GetRoots(
      string treeString,
      string[] expected)
    {
      // Arrange
      var treenumerable = TreeSerializer.Deserialize(treeString);

      // Act
      var actual =
        treenumerable
        .GetRoots()
        .ToArray();

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
