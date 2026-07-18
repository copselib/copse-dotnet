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
  public class LeaffixScanTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""                , ""                   },
          new [] { "a"               , "a"                  },
          new [] { "a(b(c,d))"       , "abcd(bcd(c,d))"     },
          new [] { "a(b(d),c(e))"    , "abdce(bd(d),ce(e))" },
          new [] { "a(b(d),c)"       , "abdc(bd(d),c)"      },
          new [] { "a(b)"            , "ab(b)"              },
          new [] { "a(b,c)"          , "abc(b,c)"           },
          new [] { "a(c),b"          , "ac(c),b"            },
          new [] { "a(c),b(d)"       , "ac(c),bd(d)"        },
          new [] { "a(c,d),b(e,f)"   , "acd(c,d),bef(e,f)"  },
          new [] { "a(d),b,c(e)"     , "ad(d),b,ce(e)"      },
          new [] { "a,b(c)"          , "a,bc(c)"            },
          new [] { "a,b(c,d)"        , "a,bcd(c,d)"         },
          new [] { "a,b,c"           , "a,b,c"              },
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

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut
        .LeaffixScan(
          (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}",
          nodeContext => nodeContext.Node)
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

    // The disclosure rule: a breadth-first-only source is accepted (the level-order arrival is
    // captured internally, the fold runs over the capture's depth-first replay, and the O(n) is
    // disclosed by the buffer return type) and must equal the explicit escalation.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void NarrowBreadthFirstSource_EqualsExplicitMaterializeThenScan(
      string treeString,
      string expectedTreeString)
    {
      var narrowSource = (IBreadthFirstTreenumerable<string>)TreeSerializer.DeserializeDepthFirstTree(treeString);

      var viaDisclosureRule = narrowSource.LeaffixScan(
        (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}",
        nodeContext => nodeContext.Node);

      var viaExplicitEscalation = TreeSerializer.DeserializeDepthFirstTree(treeString)
        .Materialize()
        .LeaffixScan(
          (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}",
          nodeContext => nodeContext.Node);

      foreach (var treeTraversalStrategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          viaExplicitEscalation.GetTraversal(treeTraversalStrategy).ToArray(),
          viaDisclosureRule.GetTraversal(treeTraversalStrategy).ToArray(),
          $"{treeTraversalStrategy} mismatch for {treeString}");
    }

    // The scan's LAYOUT is pinned to the FIRST dimension pulled (breadth-first-first lays the
    // finished scan out in level order, depth-first-first in preorder); whichever wins, both
    // dimensions must replay the same values from the one capture.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void ScanServesBothDimensionsWhicheverIsPulledFirst(
      string treeString,
      string expectedTreeString)
    {
      var expectedTree = TreeSerializer.DeserializeDepthFirstTree(expectedTreeString);

      foreach (var firstStrategy in new[] { TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.DepthFirst })
      {
        var secondStrategy =
          firstStrategy == TreeTraversalStrategy.BreadthFirst
          ? TreeTraversalStrategy.DepthFirst
          : TreeTraversalStrategy.BreadthFirst;

        var scan = TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .LeaffixScan(
            (nodeContext, children) => $"{nodeContext.Node}{string.Join("", children)}",
            nodeContext => nodeContext.Node);

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(firstStrategy).ToArray(),
          scan.GetTraversal(firstStrategy).ToArray(),
          $"{firstStrategy}-first: first drain mismatch for {treeString}");

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(secondStrategy).ToArray(),
          scan.GetTraversal(secondStrategy).ToArray(),
          $"{firstStrategy}-first: cross-dimension replay mismatch for {treeString}");
      }
    }
  }
}
