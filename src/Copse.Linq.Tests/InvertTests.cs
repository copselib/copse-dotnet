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

    // ----- The full-source mirror pins its representation to the FIRST dimension pulled
    // (breadth-first-first rides the memoized streaming mirror, depth-first-first the preorder
    // capture); whichever wins, both dimensions must replay from the one capture and the source
    // must be enumerated at most once.

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void FullSourceMirrorServesBothDimensionsWhicheverIsPulledFirst(
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

        var mirror = TreeSerializer.DeserializeDepthFirstTree(treeString).Invert();

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(firstStrategy).ToArray(),
          mirror.GetTraversal(firstStrategy).ToArray(),
          $"{firstStrategy}-first: first drain mismatch for {treeString}");

        CollectionAssert.AreEqual(
          expectedTree.GetTraversal(secondStrategy).ToArray(),
          mirror.GetTraversal(secondStrategy).ToArray(),
          $"{firstStrategy}-first: cross-dimension replay mismatch for {treeString}");
      }
    }

    [TestMethod]
    public void FullSourceMirrorEnumeratesTheSourceOnceWhicheverDimensionIsPulledFirst()
    {
      foreach (var firstStrategy in new[] { TreeTraversalStrategy.BreadthFirst, TreeTraversalStrategy.DepthFirst })
      {
        var acquisitions = 0;

        var source = Copse.Treenumerables.Tree.Defer(() =>
        {
          acquisitions++;
          return TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c(f,g))");
        });

        var mirror = source.Invert();

        mirror.GetTraversal(firstStrategy).ToArray();
        mirror.GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray();
        mirror.GetTraversal(TreeTraversalStrategy.BreadthFirst).ToArray();

        Assert.AreEqual(1, acquisitions, $"{firstStrategy}-first must enumerate the source exactly once");
      }
    }

    // The breadth-first-first arm shares the preorder arm's cost shape (decided 2026-07-13,
    // policy-audit flag 5): the whole capture builds on the FIRST replay pull -- one drain of
    // the streaming mirror straight into the level-order arrays. (It previously grew
    // tier-by-tier with Dispose completing the remainder; that laziness was only ever real for
    // a replay abandoned without disposal, and it cost a dispose-time O(n) surprise.)
    [TestMethod]
    public void BreadthFirstFirstMirrorBuildsTheWholeCaptureOnTheFirstPull()
    {
      var sourceVisits = 0;

      var mirror = TreeSerializer
        .DeserializeDepthFirstTree("a(b(d(h,i),e),c(f,g(j)))")
        .Do(visit => sourceVisits++)
        .Invert();

      int visitsAfterFirstPull;
      int visitsAfterPartialDrain;

      using (var treenumerator = mirror.GetBreadthFirstTreenumerator())
      {
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        visitsAfterFirstPull = sourceVisits;

        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        visitsAfterPartialDrain = sourceVisits;
      }

      var visitsAfterDispose = sourceVisits;

      Assert.IsTrue(visitsAfterFirstPull > 0, "the first pull must reach the source");
      Assert.AreEqual(visitsAfterFirstPull, visitsAfterPartialDrain, "the capture is complete after the first pull");
      Assert.AreEqual(visitsAfterPartialDrain, visitsAfterDispose, "dispose owes nothing");

      // The completed capture replays both dimensions without touching the source again.
      var expected = TreeSerializer.DeserializeDepthFirstTree("a(c(g(j),f),b(e,d(i,h)))");

      CollectionAssert.AreEqual(
        expected.GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray(),
        mirror.GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray());
      Assert.AreEqual(visitsAfterDispose, sourceVisits, "replays must not re-enumerate the source");
    }

    [TestMethod]
    public void BreadthFirstFirstMirrorReleasesTheSourceInsideTheFirstPull()
    {
      var resources = new List<TestResource>();

      var source = Copse.Treenumerables.Tree.Using(
        () =>
        {
          var resource = new TestResource();
          resources.Add(resource);
          return resource;
        },
        _ => TreeSerializer.DeserializeDepthFirstTree("a(b(d,e),c(f,g))"));

      var mirror = source.Invert();

      var treenumerator = mirror.GetBreadthFirstTreenumerator();
      treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);

      Assert.AreEqual(1, resources.Count, "the capture's feed acquires the resource once");
      Assert.IsTrue(
        resources[0].Disposed,
        "the build runs to completion inside the first pull and releases the source immediately");
      Assert.AreEqual(1, resources[0].DisposeCount);

      treenumerator.Dispose();

      Assert.AreEqual(1, resources[0].DisposeCount, "dispose owes nothing; the source was already released");

      // The buffer remains fully replayable from the completed capture -- no re-acquisition.
      var expected = TreeSerializer.DeserializeDepthFirstTree("a(c(g,f),b(e,d))");

      CollectionAssert.AreEqual(
        expected.GetTraversal(TreeTraversalStrategy.BreadthFirst).ToArray(),
        mirror.GetTraversal(TreeTraversalStrategy.BreadthFirst).ToArray());
      Assert.AreEqual(1, resources.Count);
    }

    private sealed class TestResource : IDisposable
    {
      public int DisposeCount { get; private set; }
      public bool Disposed => DisposeCount > 0;
      public void Dispose() => DisposeCount++;
    }
  }
}
