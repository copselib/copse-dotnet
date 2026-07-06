using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The payoff of the codegen spike at the OPERATOR layer: the async Where (Copse.Linq.Async) was
  // transcribed by Copse.CodeGen into GeneratedWhereDepthFirstTreenumerator, and that generated sync
  // Where must produce visit streams identical to the trusted public Where over the corpus -- under
  // every single-node predicate (dropping each node in turn exercises child promotion, depth
  // compression, and sibling renumbering) both plainly and under consumer skip strategies.
  [TestClass]
  public class GeneratedWhereConformanceTests
  {
    // Oracle: the trusted public Where over the engine.
    private static ITreenumerator<string> TrustedWhere(string tree, Func<NodeContext<string>, bool> predicate)
      => EngineTree.Parse(tree).Where(predicate).GetDepthFirstTreenumerator();

    // The codegen'd sync Where over the same engine inner + predicate.
    private static ITreenumerator<string> GeneratedWhere(string tree, Func<NodeContext<string>, bool> predicate)
      => new GeneratedWhereDepthFirstTreenumerator<string>(
        () => EngineTree.Parse(tree).GetDepthFirstTreenumerator(),
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
            $"generated-where {tree} [drop '{target}']");
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
          // Keep odd inner depths: drops every root and creates stacked (consecutive-ancestor) promotion.
          ("drop-even-depth", nc => nc.Position.Depth % 2 != 0),
        };

        // Drop every PAIR of distinct nodes -- when one is an ancestor of the other, this exercises
        // promotion through multiple consecutive filtered ancestors (the case single-node drops miss).
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
            $"generated-where {tree} [{desc}]");
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
                $"generated-where {tree} [drop '{target}', {strategy} on '{stratTarget}']");
            }
        }
    }
  }
}
