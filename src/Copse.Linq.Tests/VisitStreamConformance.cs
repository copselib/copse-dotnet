using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The strategy to pass to the NEXT MoveNext, chosen from the visit just emitted.
  internal delegate NodeTraversalStrategies StrategyScript(TreenumeratorMode mode, string node, int visitCount);

  // The shared visit-contract conformance harness: every ITreenumerable implementation must
  // produce visit streams identical to the engine's over the same shape, so the corpus, the
  // strategy matrix, and the lockstep comparator live HERE, once, and each implementation's
  // suite is a thin adapter that supplies treenumerator factories. (ContractTree, the flat
  // family's store and stream tiers, and the serializer round-trips all ride this.)
  internal static class VisitStreamConformance
  {
    public static readonly string[] TreeCorpus =
    {
      "",
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a,b(c)",
      "a(b,c,d)",
      "a(b(d(e)),c)",
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
      "a,b(d),c(e(f))",
    };

    public static readonly NodeTraversalStrategies[] SchedulingStrategies =
    {
      NodeTraversalStrategies.SkipNode,
      NodeTraversalStrategies.SkipDescendants,
      NodeTraversalStrategies.SkipSiblings,
      NodeTraversalStrategies.SkipNodeAndDescendants,
      NodeTraversalStrategies.SkipNodeAndSiblings,
      NodeTraversalStrategies.SkipDescendants | NodeTraversalStrategies.SkipSiblings,
      NodeTraversalStrategies.SkipAll,
    };

    public static readonly StrategyScript TraverseAll = (mode, node, visitCount) => NodeTraversalStrategies.TraverseAll;

    // The oracle: the DFS/BFS ENGINE over the same shape, via EngineTree's own parser --
    // independent of TreeSerializer, whose deserialization rides the flat family's playback
    // (part of what these suites referee).
    public static ITreenumerator<string> Engine(string tree, bool depthFirst)
      => depthFirst
        ? Copse.TestUtils.EngineTree.Parse(tree).GetDepthFirstTreenumerator()
        : Copse.TestUtils.EngineTree.Parse(tree).GetBreadthFirstTreenumerator();

    // TraverseAll over the whole corpus in one dimension.
    public static void AssertTraverseAllConforms(
      Func<string, ITreenumerator<string>> actualFactory,
      bool depthFirst,
      string family)
    {
      foreach (var tree in TreeCorpus)
        AssertSameStream(
          Engine(tree, depthFirst),
          actualFactory(tree),
          TraverseAll,
          $"{family} {tree}");
    }

    // Every strategy on every node of every corpus tree, in one dimension.
    public static void AssertStrategyMatrixConforms(
      Func<string, ITreenumerator<string>> actualFactory,
      bool depthFirst,
      string family)
    {
      foreach (var tree in TreeCorpus)
      {
        var targets = tree.Where(char.IsLetter).Select(c => c.ToString()).Distinct().ToArray();

        foreach (var target in targets)
        {
          foreach (var strategy in SchedulingStrategies)
          {
            NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
              => mode == TreenumeratorMode.SchedulingNode && node == target
                ? strategy
                : NodeTraversalStrategies.TraverseAll;

            AssertSameStream(
              Engine(tree, depthFirst),
              actualFactory(tree),
              Script,
              $"{family} {tree} [{strategy} on '{target}']");
          }
        }
      }
    }

    // The pre-enumeration convention: a fresh treenumerator, before its first MoveNext, sits at
    // the virtual forest root (see NodePosition.ForestRoot).
    public static void AssertPreEnumerationStateIsTheForestRoot(ITreenumerator<string> treenumerator, string context)
    {
      using (treenumerator)
      {
        Assert.AreEqual(NodePosition.ForestRoot, treenumerator.Position, $"[{context}] pre-enumeration Position");
        Assert.AreEqual(0, treenumerator.VisitCount, $"[{context}] pre-enumeration VisitCount");
        Assert.AreEqual(TreenumeratorMode.SchedulingNode, treenumerator.Mode, $"[{context}] pre-enumeration Mode");
      }
    }

    // Lockstep stream comparison. The strategy for each MoveNext is computed from the visit
    // just emitted (asserted equal for both sides first, so both receive the same strategy).
    public static void AssertSameStream(
      ITreenumerator<string> expected,
      ITreenumerator<string> actual,
      StrategyScript script,
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
          Assert.AreEqual(expected.Node, actual.Node, $"[{context}] step {step}: Node");
          Assert.AreEqual(expected.VisitCount, actual.VisitCount, $"[{context}] step {step}: VisitCount");
          Assert.AreEqual(expected.Position, actual.Position, $"[{context}] step {step}: Position");

          strategies = script(expected.Mode, expected.Node, expected.VisitCount);
          step++;
        }
      }
    }
  }
}
