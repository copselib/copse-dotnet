using Copse.Core;
using Copse.Linq.Generated;
using Copse.Linq.Treenumerators; // StructuralMergeDepthFirstTreenumerator (internal, via IVT) + MergeNode
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Copse.Linq.Tests
{
  // Codegen operator rollout: the async StructuralMerge (Copse.Linq.Async) -- the two-operand engine
  // behind Union/Intersection/Subtract/SymmetricDifference -- is transcribed into
  // GeneratedStructuralMergeDepthFirstTreenumerator, which must produce MergeNode visit streams
  // identical to the trusted internal StructuralMerge over operand-tree pairs (identical / asymmetric /
  // disjoint / empty-side shapes), plainly and under every consumer skip strategy.
  [TestClass]
  public class GeneratedStructuralMergeConformanceTests
  {
    // (left, right) operand pairs exercising the merge's key shapes: identical, differing subtrees,
    // one side shallower/deeper, disjoint values, forests, empty sides, asymmetric multi-level.
    private static readonly (string Left, string Right)[] Pairs =
    {
      ("a(b,c)", "a(b,c)"),
      ("a(b(d),c)", "a(b,c(e))"),
      ("a(b,c)", "a(b)"),
      ("a", "a(b,c)"),
      ("a(b,c)", "x(y,z)"),
      ("a(b,c)", ""),
      ("", "a(b,c)"),
      ("a,b,c", "a,b,c"),
      ("a(b(d,e),c)", "a(b(f),c(g,h))"),
      ("a(b(d(g),e),c)", "a(b(d,e(h)),c(i))"),
    };

    private static ITreenumerator<MergeNode<string, string>> Trusted(string left, string right)
      => new StructuralMergeDepthFirstTreenumerator<string, string>(
        () => EngineTree.Parse(left).GetDepthFirstTreenumerator(),
        () => EngineTree.Parse(right).GetDepthFirstTreenumerator());

    private static ITreenumerator<MergeNode<string, string>> Generated(string left, string right)
      => new GeneratedStructuralMergeDepthFirstTreenumerator<string, string>(
        () => EngineTree.Parse(left).GetDepthFirstTreenumerator(),
        () => EngineTree.Parse(right).GetDepthFirstTreenumerator());

    [TestMethod]
    public void TraverseAll_MatchesTrusted()
    {
      foreach (var (left, right) in Pairs)
        AssertSameMergeStream(
          Trusted(left, right), Generated(left, right),
          _ => NodeTraversalStrategies.TraverseAll,
          $"merge-dft ({left}) x ({right})");
    }

    [TestMethod]
    public void EveryConsumerStrategy_MatchesTrusted()
    {
      foreach (var (left, right) in Pairs)
        foreach (var strategy in VisitStreamConformance.SchedulingStrategies)
          AssertSameMergeStream(
            Trusted(left, right), Generated(left, right),
            mode => mode == TreenumeratorMode.SchedulingNode ? strategy : NodeTraversalStrategies.TraverseAll,
            $"merge-dft ({left}) x ({right}) [{strategy}]");
    }

    // Lockstep MergeNode-stream comparison (AssertSameStream is string-only), fully comparing the
    // merge node (both sides' presence and value) plus mode / visit count / position.
    private static void AssertSameMergeStream(
      ITreenumerator<MergeNode<string, string>> expected,
      ITreenumerator<MergeNode<string, string>> actual,
      Func<TreenumeratorMode, NodeTraversalStrategies> strategyFor,
      string context)
    {
      using (expected)
      using (actual)
      {
        var strategies = NodeTraversalStrategies.TraverseAll;
        var step = 0;

        while (true)
        {
          var expectedMoved = expected.MoveNext(strategies);
          var actualMoved = actual.MoveNext(strategies);

          Assert.AreEqual(expectedMoved, actualMoved, $"[{context}] step {step}: MoveNext disagreed");

          if (!expectedMoved)
            return;

          Assert.AreEqual(expected.Mode, actual.Mode, $"[{context}] step {step}: Mode");
          Assert.AreEqual(expected.Node.HasLeft, actual.Node.HasLeft, $"[{context}] step {step}: HasLeft");
          Assert.AreEqual(expected.Node.HasRight, actual.Node.HasRight, $"[{context}] step {step}: HasRight");
          Assert.AreEqual(expected.Node.Left, actual.Node.Left, $"[{context}] step {step}: Left");
          Assert.AreEqual(expected.Node.Right, actual.Node.Right, $"[{context}] step {step}: Right");
          Assert.AreEqual(expected.VisitCount, actual.VisitCount, $"[{context}] step {step}: VisitCount");
          Assert.AreEqual(expected.Position, actual.Position, $"[{context}] step {step}: Position");

          strategies = strategyFor(expected.Mode);
          step++;
        }
      }
    }
  }
}
