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
  public class RootfixDispatchTests
  {
    // Corpus dispatch rule: every child receives (parent's arrival + the LAST sibling's letter).
    // Reading the last sibling is deliberate -- it proves the survey saw the COMPLETE child list
    // before the first child's value was fixed, which is the operator's defining guarantee. The
    // read rides the view's O(1) indexer (2026-08-02: the build's child-index replaced span
    // hopping), so the corpus also exercises Count and the indexer at every surveyed node.
    // Roots arrive at the seed "s"; expected labels are (arrival + own letter).
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""                , ""                        },
          new [] { "a"               , "sa"                      },
          new [] { "a(b(c,d))"       , "sa(sbb(sbdc,sbdd))"      },
          new [] { "a(b(d),c(e))"    , "sa(scb(scdd),scc(scee))" },
          new [] { "a(b(d),c)"       , "sa(scb(scdd),scc)"       },
          new [] { "a(b)"            , "sa(sbb)"                 },
          new [] { "a(b,c)"          , "sa(scb,scc)"             },
          new [] { "a(c),b"          , "sa(scc),sb"              },
          new [] { "a(c),b(d)"       , "sa(scc),sb(sdd)"         },
          new [] { "a(c,d),b(e,f)"   , "sa(sdc,sdd),sb(sfe,sff)" },
          new [] { "a(d),b,c(e)"     , "sa(sdd),sb,sc(see)"      },
          new [] { "a,b(c)"          , "sa,sb(scc)"              },
          new [] { "a,b(c,d)"        , "sa,sb(sdc,sdd)"          },
          new [] { "a,b,c"           , "sa,sb,sc"                },
        };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return
        data[0].ToString() == ""
        ? "<empty-string>"
        : data[0].ToString();
    }

    private static ITreenumerable<string> LabeledDispatch(string treeString)
    {
      return
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .RootfixDispatch(
          "s",
          (parent, arrival, children) =>
          {
            var lastChildLetter = children[children.Count - 1].Node;

            foreach (var child in children)
              child.Dispatch(arrival + lastChildLetter);
          })
        .Select(node => $"{node.Accumulate}{node.Node}");
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void RootfixDispatchTest_BreadthFirst(
      string treeString,
      string expectedTreeString)
    {
      RootfixDispatchTest(treeString, expectedTreeString, TreeTraversalStrategy.BreadthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void RootfixDispatchTest_DepthFirst(
      string treeString,
      string expectedTreeString)
    {
      RootfixDispatchTest(treeString, expectedTreeString, TreeTraversalStrategy.DepthFirst);
    }

    public void RootfixDispatchTest(
      string treeString,
      string expectedTreeString,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      // Arrange
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
        LabeledDispatch(treeString)
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

    // The dispatch's LAYOUT is pinned to the FIRST dimension pulled; whichever wins, both
    // dimensions must replay the same values from the one capture (LeaffixDispatch's contract).
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

        var dispatch = LabeledDispatch(treeString);

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

    [TestMethod]
    public void SurveysRunInPreorder_AndOnlyOnInternalNodes()
    {
      var surveyedLetters = new List<string>();

      TreeSerializer
        .DeserializeDepthFirstTree("a(b(d),c)")
        .RootfixDispatch(
          "s",
          (parent, arrival, children) =>
          {
            surveyedLetters.Add(parent);
            foreach (var child in children)
              child.Dispatch(arrival);
          })
        .PreorderTraversal()
        .ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b" }, surveyedLetters); // c and d are leaves
    }

    [TestMethod]
    public void ConstructionIsDeferred_AndTheBuildRunsOnce()
    {
      var surveyCount = 0;

      var dispatch = TreeSerializer
        .DeserializeDepthFirstTree("a(b,c)")
        .RootfixDispatch<string, string>(
          "s",
          (parent, arrival, children) =>
          {
            surveyCount++;
            foreach (var child in children)
              child.Dispatch(arrival);
          });

      Assert.AreEqual(0, surveyCount); // deferred: nothing runs until the first pull

      dispatch.PreorderTraversal().ToArray();
      Assert.AreEqual(1, surveyCount);

      dispatch.LevelOrderTraversal().ToArray(); // cross-dimension replay rides the same capture
      Assert.AreEqual(1, surveyCount);
    }

    [TestMethod]
    public void MissingDispatch_Throws()
    {
      var dispatch = TreeSerializer
        .DeserializeDepthFirstTree("a(b,c)")
        .RootfixDispatch<string, string>(
          "s",
          (parent, arrival, children) =>
          {
            foreach (var child in children)
            {
              child.Dispatch(arrival);
              break; // later siblings skipped
            }
          });

      var exception = Assert.ThrowsException<InvalidOperationException>(
        () => dispatch.PreorderTraversal().ToArray());

      StringAssert.Contains(exception.Message, "without dispatching");
    }

    [TestMethod]
    public void DoubleDispatch_Throws()
    {
      var dispatch = TreeSerializer
        .DeserializeDepthFirstTree("a(b)")
        .RootfixDispatch<string, string>(
          "s",
          (parent, arrival, children) =>
          {
            // Copies of a target share the exactly-once state -- the second Dispatch throws
            // even though it runs on a fresh copy from a fresh enumeration pass.
            foreach (var child in children)
              child.Dispatch(arrival);
            foreach (var child in children)
              child.Dispatch(arrival);
          });

      var exception = Assert.ThrowsException<InvalidOperationException>(
        () => dispatch.PreorderTraversal().ToArray());

      StringAssert.Contains(exception.Message, "twice");
    }

    // The forest-correct seeding form: every root's arrival comes from rootNodeSelector against
    // that root's SOURCE context, so each tree of a forest seeds independently -- completing
    // the boundary-pair grid (RootfixScan and LeaffixDispatch each offer selector | seed).
    [TestMethod]
    public void RootNodeSelector_SeedsEachForestTreeIndependently()
    {
      var labels = TreeSerializer
        .DeserializeDepthFirstTree("a(b),c(d),e")
        .RootfixDispatch(
          (root, position) => $"[{root}@{position.SiblingIndex}]",
          (parent, arrival, children) =>
          {
            foreach (var child in children)
              child.Dispatch(arrival + child.Node);
          })
        .PreorderTraversal()
        .Select(node => $"{node.Accumulate}{node.Node}")
        .ToArray();

      CollectionAssert.AreEqual(
        new[] { "[a@0]a", "[a@0]bb", "[c@1]c", "[c@1]dd", "[e@2]e" }, labels);
    }

    // The decoration contract: the buffer holds (source value, arrival) pairs in the source's
    // shape, so downstream composition is ordinary Select/Do -- no operator flavors.
    [TestMethod]
    public void ResultDecoratesTheSourceShape()
    {
      var pairs = TreeSerializer
        .DeserializeDepthFirstTree("a(b,c)")
        .RootfixDispatch(
          0,
          (parent, arrival, children) =>
          {
            var childOrdinal = 0;
            foreach (var child in children)
              child.Dispatch(arrival * 10 + ++childOrdinal);
          })
        .PreorderTraversal()
        .Select(node => $"{node.Node}:{node.Accumulate}")
        .ToArray();

      CollectionAssert.AreEqual(new[] { "a:0", "b:1", "c:2" }, pairs);
    }

    // The view's O(1) surface, pinned: Count and the indexer agree with enumeration, bounds
    // throw, and exactly-once dispatch holds ACROSS handle copies (two fetches of children[i]
    // share the build's written-flags -- the second Dispatch throws).
    [TestMethod]
    public void DispatchTargets_IndexerCountAndSharedBackingState()
    {
      var surveyedParents = 0;

      TreeSerializer
        .DeserializeDepthFirstTree("a(b,c(e,f,g),d)")
        .RootfixDispatch(
          "s",
          (parent, arrival, children) =>
          {
            surveyedParents++;

            var enumerated = new System.Collections.Generic.List<string>();
            foreach (var child in children)
              enumerated.Add(child.Node);

            Assert.AreEqual(enumerated.Count, children.Count);
            for (var index = 0; index < children.Count; index++)
              Assert.AreEqual(enumerated[index], children[index].Node);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => children[children.Count]);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => children[-1]);

            children[0].Dispatch(arrival + children[0].Node);
            Assert.ThrowsException<InvalidOperationException>(
              () => children[0].Dispatch("again"),
              "a second Dispatch through a fresh handle copy must throw -- the backing state is shared");

            for (var index = 1; index < children.Count; index++)
              children[index].Dispatch(arrival + children[index].Node);
          })
        .PreorderTraversal()
        .ToArray();

      Assert.AreEqual(2, surveyedParents, "a and c are the only internal nodes");
    }
  }
}
