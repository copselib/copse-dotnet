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
  public class InvertTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""                , ""                 },
          new [] { "a"               , "a"                },
          new [] { "a(b(c,d))"       , "a(b(d,c))"        },
          new [] { "a(b(d),c(e))"    , "a(c(e),b(d))"     },
          new [] { "a(b(d),c)"       , "a(c,b(d))"        },
          new [] { "a(b(d,e),c(f,g))", "a(c(g,f),b(e,d))" },
          new [] { "a(b)"            , "a(b)"             },
          new [] { "a(b,c)"          , "a(c,b)"           },
          new [] { "a(c),b"          , "b,a(c)"           },
          new [] { "a(c),b(d)"       , "b(d),a(c)"        },
          new [] { "a(c,d),b(e,f)"   , "b(f,e),a(d,c)"    },
          new [] { "a(d),b,c(e)"     , "c(e),b,a(d)"      },
          new [] { "a,b(c)"          , "b(c),a"           },
          new [] { "a,b(c,d)"        , "b(d,c),a"         },
          new [] { "a,b,c"           , "c,b,a"            },
        };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return
        data[0].ToString() == ""
        ? "<empty-string>"
        : data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EnumerableToTreeTest_BreadthFirst(
      string treeString,
      string expectedTreeString)
    {
      EnumerableToTreeTest(treeString, expectedTreeString, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EnumerableToTreeTest_DepthFirst(
      string treeString,
      string expectedTreeString)
    {
      EnumerableToTreeTest(treeString, expectedTreeString, TreeTraversalStrategy.DepthFirst);
    }

    public void EnumerableToTreeTest(
      string treeString,
      string expectedTreeString,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var sut = TreeSerializer.Deserialize(treeString);

      var expected =
        TreeSerializer
        .Deserialize(expectedTreeString)
        .GetTraversal(treeTraversalStrategy)
        .ToArray();

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act: each dimension exercises its own overload -- breadth-first is the streaming
      // mirror; depth-first is only reachable through a buffer (.Memoize().Invert()), which is
      // the point of the overload set.
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        (treeTraversalStrategy == TreeTraversalStrategy.BreadthFirst
          ? sut.Invert().GetBreadthFirstTraversal()
          : sut.Memoize().Invert().GetTraversal(TreeTraversalStrategy.DepthFirst))
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      var diff = NodeVisitDiffer.Diff(expected, actual);

      Debug.WriteLine($"{Environment.NewLine}-----Diffed Values-----");
      foreach (var diffResult in diff)
        Debug.WriteLine(diffResult);

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }

    // The streaming payoff: mirror a forward-only breadth-first stream without ever holding
    // more than a level.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void StreamedSourceTest(
      string treeString,
      string expectedTreeString)
    {
      var envelope = TreeSerializer.Deserialize(treeString).Serialize(TreeTraversalStrategy.BreadthFirst);

      var expected =
        TreeSerializer
        .Deserialize(expectedTreeString)
        .GetTraversal(TreeTraversalStrategy.BreadthFirst)
        .ToArray();

      var actual =
        TreeSerializer
        .DeserializeBreadthFirst(() => new System.IO.StringReader(envelope))
        .Invert()
        .GetBreadthFirstTraversal()
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }
  }
}
