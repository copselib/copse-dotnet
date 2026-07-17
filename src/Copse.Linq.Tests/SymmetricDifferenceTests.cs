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
  // SymmetricDifference(left, right) == left.Union(right).Where(n => !n.HasLeftAndRight).
  // Union merges POSITIONALLY (never by value); SymmetricDifference keeps only nodes NOT present
  // in both trees. Because it is built on Where, removing a shared node PROMOTES its non-shared
  // descendants up to the removed node's parent (child promotion) -- the descendants survive,
  // re-parented. Contrast IntersectionTests, where the non-shared descendants of a non-shared
  // node are DROPPED (subtree pruned) instead.
  [TestClass]
  public class SymmetricDifferenceTests
  {
    public static IEnumerable<object[]> GetTestData()
    {
      // left, right, expected (surviving non-shared nodes, keyed by "{Left}{Right}" -- exactly
      // one side is present on a survivor, so the other renders as the empty string).
      return new[]
      {
        // Identical SHAPE -> every node is shared -> all removed, nothing promoted -> empty.
        new object[] { "a(b,c)",     "0(1,2)", ""        },

        // One side empty -> nothing is shared -> the whole other tree survives unchanged.
        new object[] { "a(b,c)",     "",       "a(b,c)"  },
        new object[] { "",           "0(1)",   "0(1)"    },

        // Different root counts -> the shared root 'a0' is removed (no children to promote);
        // the non-shared left root 'b' survives.
        new object[] { "a,b",        "0",      "b"       },

        // THE distinguishing case: the shared nodes ('a','b') are removed and their non-shared
        // descendant subtree ('c' with child 'd') is PROMOTED up to the root -- structure kept.
        new object[] { "a(b,c(d))",  "0(1)",   "c(d)"    },

        // Same distinction one level deeper: 'a','b' shared/removed, the non-shared leaves
        // 'c','d' are PROMOTED to become roots (their parent 'b' was removed too).
        new object[] { "a(b(c,d))",  "0(1)",   "c,d"     },
      };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
      => $"{data[0]} ⊖ {data[1]} -> {(data[2].ToString() == "" ? "<empty>" : data[2])}";

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void SymmetricDifferenceTest_DepthFirst(string leftTreeString, string rightTreeString, string expectedTreeString)
      => SymmetricDifferenceTest(leftTreeString, rightTreeString, expectedTreeString, TreeTraversalStrategy.DepthFirst);

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void SymmetricDifferenceTest_BreadthFirst(string leftTreeString, string rightTreeString, string expectedTreeString)
      => SymmetricDifferenceTest(leftTreeString, rightTreeString, expectedTreeString, TreeTraversalStrategy.BreadthFirst);

    public void SymmetricDifferenceTest(
      string leftTreeString,
      string rightTreeString,
      string expectedTreeString,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
      var leftTreenumerable = TreeSerializer.DeserializeDepthFirstTree(leftTreeString);
      var rightTreenumerable = TreeSerializer.DeserializeDepthFirstTree(rightTreeString);

      // Every survivor has exactly one side present, so "{Left}{Right}" is a single-token label
      // (the absent side is default/null and renders empty).
      var sut =
        leftTreenumerable
        .SymmetricDifference(rightTreenumerable)
        .Select(mergeNodeContext => $"{mergeNodeContext.Left}{mergeNodeContext.Right}");

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
    // dimension-preserving SymmetricDifference overload and must produce the exact same visit
    // stream as the full ITreenumerable overload over the same shapes.
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
    public void NarrowDepthFirstSymmetricDifferenceMatchesFullOverload()
    {
      const string left = "a(b,c(d))";
      const string right = "0(1)";

      // Statically IDepthFirstTreenumerable: this compiles only via the narrow overload.
      IDepthFirstTreenumerable<string> narrow =
        StreamDepthFirst(left)
        .SymmetricDifference(StreamDepthFirst(right))
        .Select(mergeNodeContext => $"{mergeNodeContext.Left}{mergeNodeContext.Right}");

      ITreenumerable<string> full =
        TreeSerializer.DeserializeDepthFirstTree(left)
        .SymmetricDifference(TreeSerializer.DeserializeDepthFirstTree(right))
        .Select(mergeNodeContext => $"{mergeNodeContext.Left}{mergeNodeContext.Right}");

      VisitStreamConformance.AssertSameStream(
        full.GetDepthFirstTreenumerator(),
        narrow.GetDepthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow SymmetricDifference DFT");
    }

    [TestMethod]
    public void NarrowBreadthFirstSymmetricDifferenceMatchesFullOverload()
    {
      const string left = "a(b,c(d))";
      const string right = "0(1)";

      IBreadthFirstTreenumerable<string> narrow =
        StreamBreadthFirst(left)
        .SymmetricDifference(StreamBreadthFirst(right))
        .Select(mergeNodeContext => $"{mergeNodeContext.Left}{mergeNodeContext.Right}");

      ITreenumerable<string> full =
        TreeSerializer.DeserializeDepthFirstTree(left)
        .SymmetricDifference(TreeSerializer.DeserializeDepthFirstTree(right))
        .Select(mergeNodeContext => $"{mergeNodeContext.Left}{mergeNodeContext.Right}");

      VisitStreamConformance.AssertSameStream(
        full.GetBreadthFirstTreenumerator(),
        narrow.GetBreadthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow SymmetricDifference BFT");
    }
  }
}
