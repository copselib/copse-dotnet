using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Codegen at the operator layer, BREADTH-FIRST dimension: the async BFT Where (Copse.Linq.Async) --
  // the library's most intricate operator (deferred/manufactured/suppressed parent visits, the O(1)
  // skipped-ancestor prefix carry) -- was transcribed by Copse.CodeGen into
  // GeneratedWhereBreadthFirstTreenumerator, which must match the trusted public Where (BFT) over the
  // corpus under every predicate and consumer skip strategy.
  [TestClass]
  public class GeneratedWhereBreadthFirstConformanceTests
  {
    // Oracle: the trusted public Where over the engine, breadth-first.
    private static ITreenumerator<string> TrustedWhere(string tree, Func<NodeContext<string>, bool> predicate)
      => EngineTree.Parse(tree).Where(predicate).GetBreadthFirstTreenumerator();

    // The codegen'd sync BFT Where over the same engine BFS inner + predicate.
    private static ITreenumerator<string> GeneratedWhere(string tree, Func<NodeContext<string>, bool> predicate)
      => new GeneratedWhereBreadthFirstTreenumerator<string>(
        () => EngineTree.Parse(tree).GetBreadthFirstTreenumerator(),
        predicate,
        NodeTraversalStrategies.SkipNode);

    private static string[] DistinctLetters(string tree)
      => tree.Where(char.IsLetter).Select(c => c.ToString()).Distinct().ToArray();

    [TestMethod]
    public void DropEachNode_TraverseAll_MatchesTrustedWhere()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var target in DistinctLetters(tree))
        {
          Func<NodeContext<string>, bool> predicate = nc => nc.Node != target;

          VisitStreamConformance.AssertSameStream(
            TrustedWhere(tree, predicate),
            GeneratedWhere(tree, predicate),
            VisitStreamConformance.TraverseAll,
            $"generated-where-bft {tree} [drop '{target}']");
        }
    }

    [TestMethod]
    public void VariedPredicates_TraverseAll_MatchesTrustedWhere()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        var letters = DistinctLetters(tree);

        var predicates = new List<(string desc, Func<NodeContext<string>, bool> predicate)>
        {
          ("keep-all", _ => true),
          ("keep-none", _ => false),
          ("drop-even-depth", nc => nc.Position.Depth % 2 != 0),
        };

        for (int i = 0; i < letters.Length; i++)
          for (int j = i + 1; j < letters.Length; j++)
          {
            var x = letters[i];
            var y = letters[j];
            predicates.Add(($"drop-'{x}'-'{y}'", nc => nc.Node != x && nc.Node != y));
          }

        foreach (var (desc, predicate) in predicates)
          VisitStreamConformance.AssertSameStream(
            TrustedWhere(tree, predicate),
            GeneratedWhere(tree, predicate),
            VisitStreamConformance.TraverseAll,
            $"generated-where-bft {tree} [{desc}]");
      }
    }

    [TestMethod]
    public void DropEachNode_EveryConsumerStrategy_MatchesTrustedWhere()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var target in DistinctLetters(tree))
        {
          Func<NodeContext<string>, bool> predicate = nc => nc.Node != target;

          foreach (var stratTarget in DistinctLetters(tree))
            foreach (var strategy in VisitStreamConformance.SchedulingStrategies)
            {
              NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
                => mode == TreenumeratorMode.SchedulingNode && node == stratTarget
                  ? strategy
                  : NodeTraversalStrategies.TraverseAll;

              VisitStreamConformance.AssertSameStream(
                TrustedWhere(tree, predicate),
                GeneratedWhere(tree, predicate),
                Script,
                $"generated-where-bft {tree} [drop '{target}', {strategy} on '{stratTarget}']");
            }
        }
    }
  }
}
