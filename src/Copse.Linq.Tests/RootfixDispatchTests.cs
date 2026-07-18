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
    // before the first child's value was fixed, which is the operator's defining guarantee.
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
          (parentContext, arrival, children) =>
          {
            var lastChildLetter = children[children.Count - 1].Node;

            foreach (var child in children)
              child.Dispatch(arrival + lastChildLetter);
          })
        .Select(dispatchNode => $"{dispatchNode.Dispatched}{dispatchNode.Value}");
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
    // dimensions must replay the same values from the one capture (LeaffixScan's contract).
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
          (parentContext, arrival, children) =>
          {
            surveyedLetters.Add(parentContext.Node);
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
          (parentContext, arrival, children) =>
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
          (parentContext, arrival, children) => children[0].Dispatch(arrival)); // later siblings skipped

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
          (parentContext, arrival, children) =>
          {
            children[0].Dispatch(arrival);
            children[0].Dispatch(arrival);
          });

      var exception = Assert.ThrowsException<InvalidOperationException>(
        () => dispatch.PreorderTraversal().ToArray());

      StringAssert.Contains(exception.Message, "twice");
    }
  }
}
