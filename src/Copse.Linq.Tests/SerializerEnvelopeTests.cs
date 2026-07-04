using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Copse.Linq.Tests
{
  // The enveloped serializer surface: round-trips in both layouts conform to the visit
  // contract (thin adapter over VisitStreamConformance -- serialize each corpus tree, stream
  // it back, lockstep against the engine), plus the header/ownership/single-shot contracts.
  [TestClass]
  public class SerializerEnvelopeTests
  {
    private static IDepthFirstTreenumerable<string> RoundTripDepthFirst(string tree)
    {
      var envelope = TreeSerializer.Deserialize(tree).Serialize(TreeTraversalStrategy.DepthFirst);
      return TreeSerializer.DeserializeDepthFirst(() => new StringReader(envelope));
    }

    private static IBreadthFirstTreenumerable<string> RoundTripBreadthFirst(string tree)
    {
      var envelope = TreeSerializer.Deserialize(tree).Serialize(TreeTraversalStrategy.BreadthFirst);
      return TreeSerializer.DeserializeBreadthFirst(() => new StringReader(envelope));
    }

    // ---------------------------------------------------------------------------------------
    // Round-trip conformance in each layout's native dimension.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void DepthFirstRoundTrip_TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(tree => RoundTripDepthFirst(tree).GetDepthFirstTreenumerator(), depthFirst: true, "dft-envelope");

    [TestMethod]
    public void BreadthFirstRoundTrip_TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(tree => RoundTripBreadthFirst(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "bft-envelope");

    [TestMethod]
    public void DepthFirstRoundTrip_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => RoundTripDepthFirst(tree).GetDepthFirstTreenumerator(), depthFirst: true, "dft-envelope");

    [TestMethod]
    public void BreadthFirstRoundTrip_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => RoundTripBreadthFirst(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "bft-envelope");

    [TestMethod]
    public void RoundTripsAreFreelyReEnumerable()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        var depthFirst = RoundTripDepthFirst(tree);

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: true), depthFirst.GetDepthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"dft first pass {tree}");
        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: true), depthFirst.GetDepthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"dft second pass {tree}");

        var breadthFirst = RoundTripBreadthFirst(tree);

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: false), breadthFirst.GetBreadthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"bft first pass {tree}");
        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine(tree, depthFirst: false), breadthFirst.GetBreadthFirstTreenumerator(), VisitStreamConformance.TraverseAll, $"bft second pass {tree}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // Payload shapes (pin the grammar so accidental format drift is visible).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void EnvelopeShapes()
    {
      Assert.AreEqual("copse/1;layout=dft\na(b(d,e),c)", TreeSerializer.Deserialize("a(b(d,e),c)").Serialize(TreeTraversalStrategy.DepthFirst));
      Assert.AreEqual("copse/1;layout=bft\na;b,c;d,e", TreeSerializer.Deserialize("a(b(d,e),c)").Serialize(TreeTraversalStrategy.BreadthFirst));
      Assert.AreEqual("copse/1;layout=bft\na,b,c", TreeSerializer.Deserialize("a,b,c").Serialize(TreeTraversalStrategy.BreadthFirst));
      Assert.AreEqual("copse/1;layout=bft\na,b,c;d|e|f;g|h|i",
        TreeSerializer.Deserialize("a(d(g)),b(e(h)),c(f(i))").Serialize(TreeTraversalStrategy.BreadthFirst));
      Assert.AreEqual("copse/1;layout=bft\n", TreeSerializer.Deserialize("").Serialize(TreeTraversalStrategy.BreadthFirst));
      Assert.AreEqual("copse/1;layout=dft\n", TreeSerializer.Deserialize("").Serialize(TreeTraversalStrategy.DepthFirst));
    }

    // ---------------------------------------------------------------------------------------
    // Header and ownership contracts.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void ReadHeaderReturnsTheLayoutAxis()
    {
      Assert.AreEqual(TreeTraversalStrategy.DepthFirst, TreeSerializer.ReadHeader(new StringReader("copse/1;layout=dft\na")));
      Assert.AreEqual(TreeTraversalStrategy.BreadthFirst, TreeSerializer.ReadHeader(new StringReader("copse/1;layout=bft\na")));
    }

    [TestMethod]
    public void LayoutMismatchThrowsAtAcquisition()
    {
      var bftEnvelope = TreeSerializer.Deserialize("a(b,c)").Serialize(TreeTraversalStrategy.BreadthFirst);
      var mistyped = TreeSerializer.DeserializeDepthFirst(() => new StringReader(bftEnvelope));

      Assert.ThrowsException<InvalidOperationException>(() => mistyped.GetDepthFirstTreenumerator());

      var dftEnvelope = TreeSerializer.Deserialize("a(b,c)").Serialize(TreeTraversalStrategy.DepthFirst);
      var mistypedDual = TreeSerializer.DeserializeBreadthFirst(() => new StringReader(dftEnvelope));

      Assert.ThrowsException<InvalidOperationException>(() => mistypedDual.GetBreadthFirstTreenumerator());
    }

    [TestMethod]
    public void MissingOrForeignHeaderThrows()
    {
      var bare = TreeSerializer.DeserializeDepthFirst(() => new StringReader("a(b,c)"));

      Assert.ThrowsException<FormatException>(() => bare.GetDepthFirstTreenumerator());
    }

    [TestMethod]
    public void SingleReaderOverloadIsSingleShot()
    {
      var envelope = TreeSerializer.Deserialize("a(b,c)").Serialize(TreeTraversalStrategy.DepthFirst);
      var singleShot = TreeSerializer.DeserializeDepthFirst(new StringReader(envelope));

      using (singleShot.GetDepthFirstTreenumerator()) { }

      Assert.ThrowsException<InvalidOperationException>(() => singleShot.GetDepthFirstTreenumerator());
    }

    [TestMethod]
    public void FileRoundTrip()
    {
      var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

      try
      {
        using (var writer = File.CreateText(path))
          TreeSerializer.Deserialize("a(b(d,e),c)").Serialize(writer, TreeTraversalStrategy.DepthFirst);

        VisitStreamConformance.AssertSameStream(
          VisitStreamConformance.Engine("a(b(d,e),c)", depthFirst: true),
          TreeSerializer.DeserializeDepthFirstFromFile(path).GetDepthFirstTreenumerator(),
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
      var envelope = "copse/1;layout=dft\n1(2,3)";
      var tree = TreeSerializer.DeserializeDepthFirst(() => new StringReader(envelope), int.Parse);

      using (var treenumerator = tree.GetDepthFirstTreenumerator())
      {
        var sum = 0;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            sum += treenumerator.Node;

        Assert.AreEqual(6, sum);
      }
    }
  }
}
