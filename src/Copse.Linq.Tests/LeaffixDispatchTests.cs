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
  public class LeaffixDispatchTests
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

    // The survey concatenates the node's letter with every child's accumulation, read through
    // the ChildAccumulations view (foreach binds to the struct enumerator -- the view is
    // deliberately not IEnumerable, so string.Join over it does not compile).
    private static string ConcatSurvey(string node, DispatchSources<string, string> children)
    {
      var concatenated = node;
      foreach (var child in children)
        concatenated += child.Accumulate;
      return concatenated;
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
        .LeaffixDispatch(
          node => node,
          ConcatSurvey)
        .Select(pairing => pairing.Accumulate)
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
    // captured internally, the survey runs over the capture's depth-first replay, and the O(n) is
    // disclosed by the buffer return type) and must equal the explicit escalation.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void NarrowBreadthFirstSource_EqualsExplicitMaterializeThenDispatch(
      string treeString,
      string expectedTreeString)
    {
      var narrowSource = (IBreadthFirstTreenumerable<string>)TreeSerializer.DeserializeDepthFirstTree(treeString);

      var viaDisclosureRule = narrowSource.LeaffixDispatch(
        node => node,
        ConcatSurvey);

      var viaExplicitEscalation = TreeSerializer.DeserializeDepthFirstTree(treeString)
        .Materialize()
        .LeaffixDispatch(
          node => node,
          ConcatSurvey);

      foreach (var treeTraversalStrategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          viaExplicitEscalation.GetTraversal(treeTraversalStrategy).ToArray(),
          viaDisclosureRule.GetTraversal(treeTraversalStrategy).ToArray(),
          $"{treeTraversalStrategy} mismatch for {treeString}");
    }

    // The dispatch's LAYOUT is pinned to the FIRST dimension pulled (breadth-first-first lays the
    // finished survey out in level order, depth-first-first in preorder); whichever wins, both
    // dimensions must replay the same values from the one capture.
    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void DispatchServesBothDimensionsWhicheverIsPulledFirst(
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

        var dispatch = TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .LeaffixDispatch(
            node => node,
            ConcatSurvey)
          .Select(pairing => pairing.Accumulate);

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(firstStrategy).ToArray(),
          dispatch.GetTraversal(firstStrategy).ToArray(),
          $"{firstStrategy}-first: first drain mismatch for {treeString}");

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(secondStrategy).ToArray(),
          dispatch.GetTraversal(secondStrategy).ToArray(),
          $"{firstStrategy}-first: cross-dimension replay mismatch for {treeString}");
      }
    }

    // Sibling-complete visibility is the tier's defining guarantee: every child's accumulation
    // is readable (twice -- Count and a second pass are both span hops) before the survey
    // returns. Each internal node's value is (own letter + LAST child's accumulation + count),
    // which is only computable if the view really exposes the complete child list.
    [TestMethod]
    public void SurveySeesAllChildrenAtOnce()
    {
      var actual = TreeSerializer
        .DeserializeDepthFirstTree("a(b(c,d),e)")
        .LeaffixDispatch(
          node => node,
          (parent, children) =>
          {
            var lastChild = children[children.Count - 1].Accumulate;

            return $"{parent}{lastChild}[{children.Count}]";
          })
        .Select(pairing => pairing.Accumulate)
        .PreorderTraversal()
        .ToArray();

      // b's children are the leaves c,d -> "bd[2]"; a's children are b's survey and the leaf e,
      // and e is last -> "ae[2]".
      CollectionAssert.AreEqual(new[] { "ae[2]", "bd[2]", "c", "d", "e" }, actual);
    }

    // The fixed-seed overload -- RootfixScan's constant-seed dual -- and the canonical
    // boundary-only aggregation it exists for: leaf count. Leaves start at the seed (1);
    // internal nodes contribute nothing of their own, just the sum of their children. This is
    // the aggregation the fold tier CANNOT express (nodeSelector cannot tell a leaf from an
    // internal node), pinning the tier split.
    [TestMethod]
    public void LeafSelector_LeafCount()
    {
      // The canonical leaf-count workload rides the SELECTOR (there is no leaffix seed flavor
      // -- THE NORTH STAR, 2026-08-05: a seed participates through the tier's callback, and
      // upward flow has no channel for one; setting leaves directly is the selector's job).
      var actual = TreeSerializer
        .DeserializeDepthFirstTree("a(b(c,d),e)")
        .LeaffixDispatch(
          _ => 1,
          (parent, children) =>
          {
            var count = 0;
            foreach (var child in children)
              count += child.Accumulate;
            return count;
          })
        .Select(pairing => pairing.Accumulate)
        .PreorderTraversal()
        .ToArray();

      // Leaves c,d,e count 1 each; b = c+d = 2; a = b+e = 3.
      CollectionAssert.AreEqual(new[] { 3, 2, 1, 1, 1 }, actual);
    }

    [TestMethod]
    public void SelectorFlavor_IsTheSurfaceForTheFringe()
    {
      // The survey-only overload died in 2026-08-05's use-case survey (fixer-less: TAccumulate
      // only inside the lambda, inference impossible -- the type-fixer-first grammar enforced
      // by the compiler). The selector names the fringe rule; internally the pass still
      // surveys every node (full participation).
      var results =
        TreeSerializer
        .DeserializeDepthFirstTree("a(b(c,d))")
        .LeaffixDispatch(node => node, ConcatSurvey)
        .PreorderTraversal()
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(new[] { "abcd", "bcd", "c", "d" }, results);
    }
  }
}
