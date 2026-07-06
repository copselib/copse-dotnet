using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
