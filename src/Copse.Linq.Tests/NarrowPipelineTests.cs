using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace Copse.Linq.Tests
{
  // End-to-end for the narrow operator overloads: a STREAMED single-dimension source
  // (DeserializeDepthFirst/DeserializeBreadthFirst) chained through the dimension-preserving
  // pipeline, drained by the narrow consumers, upgraded by the narrow Memoize/Materialize, and
  // re-serialized -- every step statically typed against the narrow interface, every visit
  // stream lockstepped against the same chain over the engine's full-citizen tree.
  [TestClass]
  public class NarrowPipelineTests
  {
    private static IDepthFirstTreenumerable<string> StreamDepthFirst(string tree)
    {
      var envelope = TreeSerializer.Deserialize(tree).Serialize(TreeTraversalStrategy.DepthFirst);
      return TreeSerializer.DeserializeDepthFirst(() => new StringReader(envelope));
    }

    private static IBreadthFirstTreenumerable<string> StreamBreadthFirst(string tree)
    {
      var envelope = TreeSerializer.Deserialize(tree).Serialize(TreeTraversalStrategy.BreadthFirst);
      return TreeSerializer.DeserializeBreadthFirst(() => new StringReader(envelope));
    }

    // ---------------------------------------------------------------------------------------
    // Dimension-preserving chains stay narrow and conform.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void DepthFirstChainOverStreamedSourceConforms()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        // Statically IDepthFirstTreenumerable at every link: this compiles only because the
        // narrow overloads exist (the ITreenumerable overloads are not applicable).
        IDepthFirstTreenumerable<string> narrowChain =
          StreamDepthFirst(tree)
          .Where(nodeContext => nodeContext.Node != "b")
          .Select(nodeContext => nodeContext.Node.ToUpperInvariant())
          .PruneAfter(nodeContext => nodeContext.Node == "D")
          .Do(_ => { })
          .Hide();

        ITreenumerable<string> engineChain =
          TreeSerializer.Deserialize(tree)
          .Where(nodeContext => nodeContext.Node != "b")
          .Select(nodeContext => nodeContext.Node.ToUpperInvariant())
          .PruneAfter(nodeContext => nodeContext.Node == "D")
          .Do(_ => { })
          .Hide();

        VisitStreamConformance.AssertSameStream(
          engineChain.GetDepthFirstTreenumerator(),
          narrowChain.GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow DFT chain {tree}");
      }
    }

    [TestMethod]
    public void BreadthFirstChainOverStreamedSourceConforms()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        IBreadthFirstTreenumerable<string> narrowChain =
          StreamBreadthFirst(tree)
          .Where(nodeContext => nodeContext.Node != "b")
          .Select(nodeContext => nodeContext.Node.ToUpperInvariant())
          .PruneAfter(nodeContext => nodeContext.Node == "D")
          .Do(_ => { })
          .Hide();

        ITreenumerable<string> engineChain =
          TreeSerializer.Deserialize(tree)
          .Where(nodeContext => nodeContext.Node != "b")
          .Select(nodeContext => nodeContext.Node.ToUpperInvariant())
          .PruneAfter(nodeContext => nodeContext.Node == "D")
          .Do(_ => { })
          .Hide();

        VisitStreamConformance.AssertSameStream(
          engineChain.GetBreadthFirstTreenumerator(),
          narrowChain.GetBreadthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"narrow BFT chain {tree}");
      }
    }

    [TestMethod]
    public void NarrowSetOperationsConform()
    {
      const string left = "a(b(d,e,f),c(g,h,i))";
      const string right = "a(x,b(y))";

      IDepthFirstTreenumerable<string> narrowUnion =
        StreamDepthFirst(left).Union(StreamDepthFirst(right)).Select(nodeContext => nodeContext.Node.ToString());

      ITreenumerable<string> engineUnion =
        TreeSerializer.Deserialize(left).Union(TreeSerializer.Deserialize(right)).Select(nodeContext => nodeContext.Node.ToString());

      VisitStreamConformance.AssertSameStream(
        engineUnion.GetDepthFirstTreenumerator(),
        narrowUnion.GetDepthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow Union DFT");

      IBreadthFirstTreenumerable<string> narrowSubtract =
        StreamBreadthFirst(left).Subtract(StreamBreadthFirst(right));

      ITreenumerable<string> engineSubtract =
        TreeSerializer.Deserialize(left).Subtract(TreeSerializer.Deserialize(right));

      VisitStreamConformance.AssertSameStream(
        engineSubtract.GetBreadthFirstTreenumerator(),
        narrowSubtract.GetBreadthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "narrow Subtract BFT");
    }

    // ---------------------------------------------------------------------------------------
    // Narrow consumers.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void NarrowConsumersMatchEngine()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        var engine = TreeSerializer.Deserialize(tree);

        Assert.AreEqual(engine.CountNodes(), StreamDepthFirst(tree).CountNodes(), $"DFT CountNodes {tree}");
        Assert.AreEqual(engine.CountNodes(), StreamBreadthFirst(tree).CountNodes(), $"BFT CountNodes {tree}");
        Assert.AreEqual(engine.CountTrees(), StreamDepthFirst(tree).CountTrees(), $"CountTrees {tree}");
        CollectionAssert.AreEqual(engine.GetLeaves().ToArray(), StreamDepthFirst(tree).GetLeaves().ToArray(), $"GetLeaves {tree}");
        CollectionAssert.AreEqual(engine.PreOrderTraversal().ToArray(), StreamDepthFirst(tree).PreOrderTraversal().ToArray(), $"PreOrder {tree}");
        CollectionAssert.AreEqual(engine.PostOrderTraversal().ToArray(), StreamDepthFirst(tree).PostOrderTraversal().ToArray(), $"PostOrder {tree}");
        CollectionAssert.AreEqual(engine.LevelOrderTraversal().ToArray(), StreamBreadthFirst(tree).LevelOrderTraversal().ToArray(), $"LevelOrder {tree}");
        CollectionAssert.AreEqual(
          engine.GetLevels().Select(level => string.Join("~", level)).ToArray(),
          StreamBreadthFirst(tree).GetLevels().Select(level => string.Join("~", level)).ToArray(),
          $"GetLevels {tree}");
        Assert.AreEqual(engine.Serialize(), StreamDepthFirst(tree).Serialize(), $"bare Serialize {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // The upgrade ops: Memoize/Materialize buy back the composite.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void NarrowMemoizeBuysBackTheOtherDimension()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        // A depth-first-only stream, memoized, serves BREADTH-first replays (cross-order over
        // the capture) -- the escalation, now visible in code.
        ITreenumerable<string> upgraded = StreamDepthFirst(tree).Memoize();

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: false),
          upgraded.GetBreadthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"memoized DFT stream, BFT replay {tree}");

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: true),
          upgraded.GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"memoized DFT stream, DFT replay {tree}");

        // And the dual: a breadth-first-only stream's ONLY road to the depth-first dimension.
        ITreenumerable<string> upgradedDual = StreamBreadthFirst(tree).Memoize();

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: true),
          upgradedDual.GetDepthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"memoized BFT stream, DFT replay {tree}");

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: false),
          upgradedDual.GetBreadthFirstTreenumerator(),
          VisitStreamConformance.TraverseAll,
          $"memoized BFT stream, BFT replay {tree}");
      }
    }

    [TestMethod]
    public void NarrowMaterializeCompletesTheCapture()
    {
      var materialized = StreamDepthFirst("a(b(d,e),c)").Materialize();

      Assert.IsTrue(materialized.IsComplete);
      Assert.AreEqual(5, materialized.GetBufferedCount(TreeTraversalStrategy.DepthFirst));

      VisitStreamConformance.AssertSameStream(
        VisitStreamConformance.Engine("a(b(d,e),c)", depthFirst: false),
        materialized.GetBreadthFirstTreenumerator(),
        VisitStreamConformance.TraverseAll,
        "materialized narrow, BFT replay");
    }

    // ---------------------------------------------------------------------------------------
    // Narrow re-serialization: stream in, transform, stream out -- no full tree ever resident.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void NarrowSerializeRoundTrips()
    {
      var envelope = TreeSerializer.Deserialize("a(b(d,e),c)").Serialize(TreeTraversalStrategy.BreadthFirst);

      IBreadthFirstTreenumerable<string> filtered =
        TreeSerializer.DeserializeBreadthFirst(() => new StringReader(envelope))
        .Where(nodeContext => nodeContext.Node != "d");

      using (var writer = new StringWriter())
      {
        filtered.Serialize(writer);
        Assert.AreEqual("copse/1;layout=bft\na;b,c;e", writer.ToString());
      }
    }
  }
}
