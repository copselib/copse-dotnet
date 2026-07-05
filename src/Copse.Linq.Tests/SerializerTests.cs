using Copse.Core;
using Copse.SimpleSerializer;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The header-free serializer: the method name declares the stored layout (no envelope), the
  // STRING tier returns a full ITreenumerable (random access), and the STREAM tier returns the
  // narrow interface (forward-only, bounded). Round-trips conform via the shared visit-contract
  // battery; wrong-layout input fails fast; payload shapes are pinned.
  [TestClass]
  public class SerializerTests
  {
    // Round-trip factories: serialize each corpus tree in a layout, read it back.
    private static ITreenumerable<string> StringDepthFirst(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(EngineTree.Parse(tree).SerializeDepthFirstTree());

    private static ITreenumerable<string> StringBreadthFirst(string tree)
      => TreeSerializer.DeserializeBreadthFirstTree(EngineTree.Parse(tree).SerializeBreadthFirstTree());

    private static IDepthFirstTreenumerable<string> StreamDepthFirst(string tree)
    {
      var payload = EngineTree.Parse(tree).SerializeDepthFirstTree();
      return TreeSerializer.DeserializeDepthFirstTree(() => new StringReader(payload));
    }

    private static IBreadthFirstTreenumerable<string> StreamBreadthFirst(string tree)
    {
      var payload = EngineTree.Parse(tree).SerializeBreadthFirstTree();
      return TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(payload));
    }

    // ---------------------------------------------------------------------------------------
    // Round-trip conformance (shared battery).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void StringDepthFirst_EveryNodeEveryStrategy()
    {
      // A string is random-access, so BOTH dimensions of the depth-first-serialized tree work.
      VisitStreamConformance.AssertStrategyMatrixConforms(tree => StringDepthFirst(tree).GetDepthFirstTreenumerator(), depthFirst: true, "dft-string");
      VisitStreamConformance.AssertStrategyMatrixConforms(tree => StringDepthFirst(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "dft-string cross");
    }

    [TestMethod]
    public void StringBreadthFirst_EveryNodeEveryStrategy()
    {
      VisitStreamConformance.AssertStrategyMatrixConforms(tree => StringBreadthFirst(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "bft-string");
      VisitStreamConformance.AssertStrategyMatrixConforms(tree => StringBreadthFirst(tree).GetDepthFirstTreenumerator(), depthFirst: true, "bft-string cross");
    }

    [TestMethod]
    public void StreamDepthFirst_EveryNodeEveryStrategy()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => StreamDepthFirst(tree).GetDepthFirstTreenumerator(), depthFirst: true, "dft-stream");

    [TestMethod]
    public void StreamBreadthFirst_EveryNodeEveryStrategy()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => StreamBreadthFirst(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "bft-stream");

    [TestMethod]
    public void StreamsAreReEnumerable()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        var depthFirst = StreamDepthFirst(tree);
        VisitStreamConformance.AssertSameStream(VisitStreamConformance.Engine(tree, true), depthFirst.GetDepthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"dft stream pass 1 {tree}");
        VisitStreamConformance.AssertSameStream(VisitStreamConformance.Engine(tree, true), depthFirst.GetDepthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"dft stream pass 2 {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Payload shapes (bare -- no header -- so drift is visible).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void PayloadShapes()
    {
      Assert.AreEqual("a(b(d,e),c)", EngineTree.Parse("a(b(d,e),c)").SerializeDepthFirstTree());
      Assert.AreEqual("a;b,c;d,e", EngineTree.Parse("a(b(d,e),c)").SerializeBreadthFirstTree());
      Assert.AreEqual("a,b,c", EngineTree.Parse("a,b,c").SerializeBreadthFirstTree());
      Assert.AreEqual("a,b,c;d|e|f;g|h|i", EngineTree.Parse("a(d(g)),b(e(h)),c(f(i))").SerializeBreadthFirstTree());
      Assert.AreEqual("", EngineTree.Parse("").SerializeBreadthFirstTree());
      Assert.AreEqual("", EngineTree.Parse("").SerializeDepthFirstTree());
    }

    // ---------------------------------------------------------------------------------------
    // Wrong-layout detection: the other grammar's structural character fails fast (the
    // replacement for the retired layout header).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void DepthFirstStringRejectsLevelOrderStructure()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("a;b,c");
      Assert.ThrowsException<FormatException>(() => tree.GetDepthFirstTreenumerator().MoveNext(NodeTraversalStrategies.TraverseAll));
    }

    [TestMethod]
    public void BreadthFirstStringRejectsDepthFirstStructure()
    {
      var tree = TreeSerializer.DeserializeBreadthFirstTree("a(b,c)");
      Assert.ThrowsException<FormatException>(() =>
      {
        using (var t = tree.GetBreadthFirstTreenumerator())
          while (t.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
      });
    }

    [TestMethod]
    public void DepthFirstStreamRejectsLevelOrderStructure()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree(() => new StringReader("a;b,c"));
      Assert.ThrowsException<FormatException>(() =>
      {
        using (var t = tree.GetDepthFirstTreenumerator())
          while (t.MoveNext(NodeTraversalStrategies.TraverseAll)) { }
      });
    }

    // ---------------------------------------------------------------------------------------
    // The escalation is explicit: a breadth-first stream has no depth-first dimension on its
    // type; Memoize buys it back (and no hidden buffering happens without it).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void BreadthFirstStreamReachesDepthFirstOnlyThroughMemoize()
    {
      var payload = EngineTree.Parse("a(b(d,e),c)").SerializeBreadthFirstTree();

      IBreadthFirstTreenumerable<string> stream = TreeSerializer.DeserializeBreadthFirstTree(() => new StringReader(payload));

      ITreenumerable<string> upgraded = stream.Memoize();

      VisitStreamConformance.AssertSameStream(
        VisitStreamConformance.Engine("a(b(d,e),c)", depthFirst: true),
        upgraded.GetDepthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "memoized bft stream, dft replay");
    }

    // ---------------------------------------------------------------------------------------
    // File + value map.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void FileRoundTrip()
    {
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

      try
      {
        using (var writer = File.CreateText(path))
          EngineTree.Parse("a(b(d,e),c)").SerializeDepthFirstTree(writer);

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine("a(b(d,e),c)", depthFirst: true),
          TreeSerializer.DeserializeDepthFirstTreeFromFile(path).GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          "file round trip");
      }
      finally
      {
        File.Delete(path);
      }
    }

    [TestMethod]
    public void ValueMapIsApplied()
    {
      var tree = TreeSerializer.DeserializeDepthFirstTree("1(2,3)", int.Parse);

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        var sum = 0;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            sum += treenumerator.Node;

        Assert.AreEqual(6, sum);
      }
    }

    [TestMethod]
    public void DeserializeStringParsesLazily()
    {
      var payload = "a(b(c(d(e(f(g(h)))))))";
      var mapCalls = 0;

      var tree = TreeSerializer.DeserializeDepthFirstTree(payload, value => { mapCalls++; return value; });

      Assert.AreEqual(0, mapCalls, "composition must parse nothing");

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
        treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll);
      }

      Assert.IsTrue(mapCalls <= 3, $"expected a small parsed prefix, but the map ran {mapCalls} times");
    }
  }
}
