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
      var sut = TreeSerializer.DeserializeDepthFirstTree(treeString);

      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(treeTraversalStrategy)
        .ToArray();

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act: a full source's Invert now returns a completed buffer (both dimensions), so either
      // traversal is reachable directly -- no explicit .Memoize() needed. (Narrow BFT-only
      // streaming is covered by StreamedSourceTest below.)
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut.Invert()
        .GetTraversal(treeTraversalStrategy)
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
      var envelope = TreeSerializer.DeserializeDepthFirstTree(treeString).SerializeBreadthFirstTree();

      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(TreeTraversalStrategy.BreadthFirst)
        .ToArray();

      var actual =
        TreeSerializer
        .DeserializeBreadthFirstTree(() => new System.IO.StringReader(envelope))
        .Invert()
        .GetBreadthFirstTraversal()
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }

    // A narrow depth-first-only stream can now Invert directly: it Materializes internally and
    // returns a completed buffer (no forced .Memoize().Invert()), so BOTH mirror dimensions are
    // reachable -- the capability the disclose-on-output redesign adds for narrow DFT sources.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void NarrowDepthFirstStreamCanInvert(
      string treeString,
      string expectedTreeString)
    {
      var envelope = TreeSerializer.DeserializeDepthFirstTree(treeString).SerializeDepthFirstTree();

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        var expected =
          TreeSerializer.DeserializeDepthFirstTree(expectedTreeString).GetTraversal(strategy).ToArray();

        // A fresh forward-only stream each time (Invert consumes it once to capture).
        var actual =
          TreeSerializer
          .DeserializeDepthFirstTree(() => new System.IO.StringReader(envelope))
          .Invert()
          .GetTraversal(strategy)
          .ToArray();

        CollectionAssert.AreEqual(expected, actual, $"{strategy} {treeString}");
      }
    }
  }
}
