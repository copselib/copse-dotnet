using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  [TestClass]
  public class OrderChildrenByTests
  {
    // (source, expected ascending, expected descending), keyed by the node string itself.
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""                , ""                , ""                },
          new [] { "a"               , "a"               , "a"               },
          new [] { "b,a,c"           , "a,b,c"           , "c,b,a"           },
          new [] { "a(c,b)"          , "a(b,c)"          , "a(c,b)"          },
          new [] { "a(b,c),d"        , "a(b,c),d"        , "d,a(c,b)"        },
          new [] { "b(d,c),a(z,y)"   , "a(y,z),b(c,d)"   , "b(d,c),a(z,y)"   },
          new [] { "b(d(f),c),a"     , "a,b(c,d(f))"     , "b(d(f),c),a"     },
          new [] { "a(c(e,d),b)"     , "a(b,c(d,e))"     , "a(c(e,d),b)"     },
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
    public void OrdersEverySiblingGroup_DepthFirst(string treeString, string expectedAscending, string expectedDescending)
    {
      OrdersEverySiblingGroup(treeString, expectedAscending, expectedDescending, TreeTraversalStrategy.DepthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void OrdersEverySiblingGroup_BreadthFirst(string treeString, string expectedAscending, string expectedDescending)
    {
      OrdersEverySiblingGroup(treeString, expectedAscending, expectedDescending, TreeTraversalStrategy.BreadthFirst);
    }

    private static void OrdersEverySiblingGroup(
      string treeString,
      string expectedAscending,
      string expectedDescending,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      CollectionAssert.AreEqual(
        TreeSerializer.DeserializeDepthFirstTree(expectedAscending).GetTraversal(treeTraversalStrategy).ToArray(),
        TreeSerializer.DeserializeDepthFirstTree(treeString)
          .OrderChildrenBy(nodeContext => nodeContext.Node)
          .GetTraversal(treeTraversalStrategy).ToArray(),
        $"ascending mismatch for {treeString}");

      CollectionAssert.AreEqual(
        TreeSerializer.DeserializeDepthFirstTree(expectedDescending).GetTraversal(treeTraversalStrategy).ToArray(),
        TreeSerializer.DeserializeDepthFirstTree(treeString)
          .OrderChildrenByDescending(nodeContext => nodeContext.Node)
          .GetTraversal(treeTraversalStrategy).ToArray(),
        $"descending mismatch for {treeString}");
    }

    // THE LAW this operator was held to: Invert is OrderChildrenByDescending over the SOURCE
    // sibling index (a stable descending sort of strictly increasing keys is an exact reversal,
    // roots included). Invert keeps its specialized zero-allocation build -- this pins that the
    // general operator subsumes it semantically.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void Invert_IsOrderChildrenByDescendingSiblingIndex(string treeString, string expectedAscending, string expectedDescending)
    {
      foreach (var treeTraversalStrategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        CollectionAssert.AreEqual(
          TreeSerializer.DeserializeDepthFirstTree(treeString)
            .Invert()
            .GetTraversal(treeTraversalStrategy).ToArray(),
          TreeSerializer.DeserializeDepthFirstTree(treeString)
            .OrderChildrenByDescending(nodeContext => nodeContext.Position.SiblingIndex)
            .GetTraversal(treeTraversalStrategy).ToArray(),
          $"{treeTraversalStrategy} mismatch for {treeString}");
      }
    }

    [TestMethod]
    public void OrderingIsStable_BothDirections_EqualKeysKeepSourceOrder()
    {
      // Keys collide on the first letter: ax and ay tie, so they keep their source order in BOTH
      // directions (LINQ ordering semantics -- descending does not reverse ties).
      var ascending =
        TreeSerializer.DeserializeDepthFirstTree("ax,ay,b")
        .OrderChildrenBy(nodeContext => nodeContext.Node[0])
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { "ax", "ay", "b" }, ascending);

      var descending =
        TreeSerializer.DeserializeDepthFirstTree("ax,ay,b")
        .OrderChildrenByDescending(nodeContext => nodeContext.Node[0])
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { "b", "ax", "ay" }, descending);
    }

    [TestMethod]
    public void ExplicitComparer_IsHonored()
    {
      // The default string comparer is linguistic (a before B); explicit Ordinal flips it
      // ('B' is 66, 'a' is 97).
      CollectionAssert.AreEqual(
        new[] { "a", "B" },
        TreeSerializer.DeserializeDepthFirstTree("B,a")
          .OrderChildrenBy(nodeContext => nodeContext.Node)
          .PreorderTraversal().ToArray());

      CollectionAssert.AreEqual(
        new[] { "B", "a" },
        TreeSerializer.DeserializeDepthFirstTree("B,a")
          .OrderChildrenBy(nodeContext => nodeContext.Node, StringComparer.Ordinal)
          .PreorderTraversal().ToArray());
    }

    [TestMethod]
    public void KeySelector_SeesSourcePositions_AndRunsOncePerNode()
    {
      var seenSiblingIndexesByNode = new Dictionary<string, int>();

      var ordered =
        TreeSerializer.DeserializeDepthFirstTree("b,a")
        .OrderChildrenBy(nodeContext =>
        {
          seenSiblingIndexesByNode.Add(nodeContext.Node, nodeContext.Position.SiblingIndex); // Add throws on a re-run
          return nodeContext.Node;
        });

      Assert.AreEqual(0, seenSiblingIndexesByNode.Count); // deferred: nothing runs until the first pull

      ordered.PreorderTraversal().ToArray();
      ordered.LevelOrderTraversal().ToArray(); // cross-dimension replay rides the same capture

      Assert.AreEqual(0, seenSiblingIndexesByNode["b"]); // SOURCE positions, pre-ordering
      Assert.AreEqual(1, seenSiblingIndexesByNode["a"]);
    }

    // The ordering's LAYOUT is pinned to the first dimension pulled; whichever wins, both
    // dimensions must replay the same values from the one capture.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void ServesBothDimensionsWhicheverIsPulledFirst(string treeString, string expectedAscending, string expectedDescending)
    {
      var expectedTree = TreeSerializer.DeserializeDepthFirstTree(expectedAscending);

      foreach (var firstStrategy in new[] { TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.DepthFirst })
      {
        var secondStrategy =
          firstStrategy == TreeTraversalStrategy.BreadthFirst
          ? TreeTraversalStrategy.DepthFirst
          : TreeTraversalStrategy.BreadthFirst;

        var ordered =
          TreeSerializer.DeserializeDepthFirstTree(treeString)
          .OrderChildrenBy(nodeContext => nodeContext.Node);

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(firstStrategy).ToArray(),
          ordered.GetTraversal(firstStrategy).ToArray(),
          $"{firstStrategy}-first: first drain mismatch for {treeString}");

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(secondStrategy).ToArray(),
          ordered.GetTraversal(secondStrategy).ToArray(),
          $"{firstStrategy}-first: cross-dimension replay mismatch for {treeString}");
      }
    }
  }
}
