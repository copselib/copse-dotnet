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
  public class GetBranchesTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      yield return new object[]
      {
        "a",
        new[]
        {
          new[] { "a" }
        }
      };
      yield return new object[]
      {
        "a,b,c",
        new[]
        {
          new[] { "a" },
          new[] { "b" },
          new[] { "c" }
        }
      };
      yield return new object[]
      {
        "a(b,c)",
        new[]
        {
          new[] { "a", "b" },
          new[] { "a", "c" }
        }
      };
      yield return new object[]
      {
        "a(b(e,f,g),c(h,i,j))",
        new[]
        {
          new[] { "a", "b", "e" },
          new[] { "a", "b", "f" },
          new[] { "a", "b", "g" },
          new[] { "a", "c", "h" },
          new[] { "a", "c", "i" },
          new[] { "a", "c", "j" },
        }
      };
      yield return new object[]
      {
        "a(b(c))",
        new[]
        {
          new[] { "a", "b", "c" }
        }
      };
      yield return new object[]
      {
        "a(b,c),d(e,f)",
        new[]
        {
          new[] { "a", "b" },
          new[] { "a", "c" },
          new[] { "d", "e" },
          new[] { "d", "f" }
        }
      };
      yield return new object[]
      {
        "a,b(c),d(e(f))",
        new[]
        {
          new[] { "a" },
          new[] { "b", "c" },
          new[] { "d", "e", "f" }
        }
      };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void GetBranches(
      string treeString,
      string[][] expected)
    {
      // Arrange
      var treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);

      // Act
      var actual =
        treenumerable
        .GetBranches()
        .Do(branch => Debug.WriteLine(string.Join(", ", branch)))
        .ToArray();

      // Assert
      Assert.AreEqual(expected.Length, actual.Length);
      for (int i = 0; i < expected.Length; i++)
        CollectionAssert.AreEqual(expected[i], actual[i]);
    }
  }
}
