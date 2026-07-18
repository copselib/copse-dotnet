using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  // Intersection(left, right) == left.Union(right).PruneBefore(nc => !nc.Node.HasLeftAndRight).
  // Union merges POSITIONALLY (never by value); Intersection keeps only nodes present in BOTH
  // trees and, at the first non-shared node, PRUNES THE WHOLE SUBTREE -- the non-shared node's
  // descendants never appear. (A descendant of a non-shared node is itself always non-shared,
  // because the other side has no node at that position; the prune just drops the subtree
  // outright.) Contrast SymmetricDifferenceTests, where the removed nodes' descendants are
  // PROMOTED rather than dropped.
  [TestClass]
  public class IntersectionTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      // left, right, expected (the surviving shared nodes, keyed by their LEFT value)
      return new[]
      {
        // Identical SHAPE (values differ; union is positional, not by value) -> whole tree shared.
        new object[] { "a(b,c)",     "0(1,2)", "a(b,c)" },

        // One side empty -> nothing is shared -> empty.
        new object[] { "a(b,c)",     "",       ""       },
        new object[] { "",           "0(1)",   ""       },

        // Different root counts -> only the positionally shared prefix survives; the extra
        // left root 'b' is non-shared and pruned.
        new object[] { "a,b",        "0",      "a"      },

        // THE distinguishing case: a shared node ('a','b') owns non-shared descendants ('c','d').
        // Intersection PRUNES the c-subtree entirely -- 'd' must NOT appear.
        new object[] { "a(b,c(d))",  "0(1)",   "a(b)"   },

        // Same distinction one level deeper: 'a','b' shared, 'c','d' non-shared leaves -> pruned.
        new object[] { "a(b(c,d))",  "0(1)",   "a(b)"   },
      };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
      => $"{data[0]} ∩ {data[1]} -> {(data[2].ToString() == "" ? "<empty>" : data[2])}";

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void IntersectionTest_DepthFirst(string leftTreeString, string rightTreeString, string expectedTreeString)
      => IntersectionTest(leftTreeString, rightTreeString, expectedTreeString, TreeTraversalStrategy.DepthFirst);

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void IntersectionTest_BreadthFirst(string leftTreeString, string rightTreeString, string expectedTreeString)
      => IntersectionTest(leftTreeString, rightTreeString, expectedTreeString, TreeTraversalStrategy.BreadthFirst);

    public void IntersectionTest(
      string leftTreeString,
      string rightTreeString,
      string expectedTreeString,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var leftTreenumerable = TreeSerializer.DeserializeDepthFirstTree(leftTreeString);
      var rightTreenumerable = TreeSerializer.DeserializeDepthFirstTree(rightTreeString);

      // Every surviving Intersection node is shared (HasLeftAndRight), so its LEFT value is a
      // faithful, single-token label to compare structure against.
      var sut =
        leftTreenumerable
        .Intersection(rightTreenumerable)
        .Select(mergeNodeContext => mergeNodeContext.Left);

      Func<NodeContext<string>, NodeTraversalStrategies> traverseAll =
        _ => NodeTraversalStrategies.TraverseAll;

      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(treeTraversalStrategy, traverseAll)
        .ToArray();

      Debug.WriteLine($"Left Tree: {leftTreeString}");
      Debug.WriteLine($"Right Tree: {rightTreeString}");

      Debug.WriteLine("-----Expected Values-----");
      foreach (var value in expected)
        Debug.WriteLine(value);

      // Act
      Debug.WriteLine($"{Environment.NewLine}-----Actual Values-----");
      var actual =
        sut
        .GetTraversal(treeTraversalStrategy, traverseAll)
        .Do(visit => Debug.WriteLine(visit))
        .ToArray();

      var diff = NodeVisitDiffer.Diff(expected, actual);

      Debug.WriteLine($"{Environment.NewLine}-----Diffed Values-----");
      foreach (var diffResult in diff)
        Debug.WriteLine(diffResult);

      // Assert
      CollectionAssert.AreEqual(expected, actual);
    }

    // -------------------------------------------------------------------------------------------
    // Narrow (single-dimension) overloads: a STREAMED, forward-only source flows through the
    // dimension-preserving Intersection overload and must produce the exact same visit stream as
    // the full ITreenumerable overload over the same shapes.
    // -------------------------------------------------------------------------------------------

    private static IDepthFirstTreenumerable<string> StreamDepthFirst(string tree)
    {
      var envelope = TreeSerializer.DeserializeDepthFirstTree(tree).SerializeDepthFirstTree();
      return TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(envelope));
    }

    private static IBreadthFirstTreenumerable<string> StreamBreadthFirst(string tree)
    {
      var envelope = TreeSerializer.DeserializeDepthFirstTree(tree).SerializeBreadthFirstTree();
      return TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(envelope));
    }

    [TestMethod]
    public void NarrowDepthFirstIntersectionMatchesFullOverload()
    {
      const string left = "a(b,c(d))";
      const string right = "0(1)";

      // Statically IDepthFirstTreenumerable: this compiles only via the narrow overload.
      IDepthFirstTreenumerable<string> narrow =
        StreamDepthFirst(left)
        .Intersection(StreamDepthFirst(right))
        .Select(mergeNodeContext => mergeNodeContext.Left);

      ITreenumerable<string> full =
        TreeSerializer.DeserializeDepthFirstTree(left)
        .Intersection(TreeSerializer.DeserializeDepthFirstTree(right))
        .Select(mergeNodeContext => mergeNodeContext.Left);

      VisitStreamConformance.AssertSameStream(
        full.GetDepthFirstTreenumerator(),
        narrow.GetDepthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow Intersection DFT");
    }

    [TestMethod]
    public void NarrowBreadthFirstIntersectionMatchesFullOverload()
    {
      const string left = "a(b,c(d))";
      const string right = "0(1)";

      IBreadthFirstTreenumerable<string> narrow =
        StreamBreadthFirst(left)
        .Intersection(StreamBreadthFirst(right))
        .Select(mergeNodeContext => mergeNodeContext.Left);

      ITreenumerable<string> full =
        TreeSerializer.DeserializeDepthFirstTree(left)
        .Intersection(TreeSerializer.DeserializeDepthFirstTree(right))
        .Select(mergeNodeContext => mergeNodeContext.Left);

      VisitStreamConformance.AssertSameStream(
        full.GetBreadthFirstTreenumerator(),
        narrow.GetBreadthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow Intersection BFT");
    }
  }
}
