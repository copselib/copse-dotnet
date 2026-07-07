using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Copse.Linq.Tests
{
  // Codegen operator rollout: the async RootfixScan (Copse.Linq.Async, DFT + BFT) is transcribed into
  // GeneratedRootfixScan{Depth,Breadth}FirstTreenumerator, which must produce visit streams identical
  // to the trusted public RootfixScan over the corpus -- plainly and under every consumer skip
  // strategy (RootfixScan tracks skipped ancestors so a promoted child still accumulates correctly).
  [TestClass]
  public class GeneratedRootfixScanConformanceTests
  {
    // Root-to-node path concatenation: a simple, order-sensitive accumulator over the string corpus.
    private static readonly Func<NodeContext<string>, NodeContext<string>, string> Concat =
      (acc, node) => acc.Node + node.Node;

    private static ITreenumerator<string> TrustedDft(string tree)
      => EngineTree.Parse(tree).RootfixScan(Concat, "").GetDepthFirstTreenumerator();

    private static ITreenumerator<string> GeneratedDft(string tree)
      => new GeneratedRootfixScanDepthFirstTreenumerator<string, string>(
        () => EngineTree.Parse(tree).GetDepthFirstTreenumerator(), Concat, "");

    private static ITreenumerator<string> TrustedBft(string tree)
      => EngineTree.Parse(tree).RootfixScan(Concat, "").GetBreadthFirstTreenumerator();

    private static ITreenumerator<string> GeneratedBft(string tree)
      => new GeneratedRootfixScanBreadthFirstTreenumerator<string, string>(
        () => EngineTree.Parse(tree).GetBreadthFirstTreenumerator(), Concat, "");

    [TestMethod]
    public void DepthFirst_TraverseAll_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        VisitStreamConformance.AssertSameStream(
          TrustedDft(tree), GeneratedDft(tree), VisitStreamConformance.TraverseAll, $"rootfixscan-dft {tree}");
    }

    [TestMethod]
    public void BreadthFirst_TraverseAll_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        VisitStreamConformance.AssertSameStream(
          TrustedBft(tree), GeneratedBft(tree), VisitStreamConformance.TraverseAll, $"rootfixscan-bft {tree}");
    }

    [TestMethod]
    public void DepthFirst_EveryConsumerStrategy_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var strategy in VisitStreamConformance.SchedulingStrategies)
        {
          StrategyScript script =
            (mode, node, visitCount) => mode == TreenumeratorMode.SchedulingNode ? strategy : NodeTraversalStrategies.TraverseAll;

          VisitStreamConformance.AssertSameStream(
            TrustedDft(tree), GeneratedDft(tree), script, $"rootfixscan-dft {tree} [{strategy}]");
        }
    }

    [TestMethod]
    public void BreadthFirst_EveryConsumerStrategy_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var strategy in VisitStreamConformance.SchedulingStrategies)
        {
          StrategyScript script =
            (mode, node, visitCount) => mode == TreenumeratorMode.SchedulingNode ? strategy : NodeTraversalStrategies.TraverseAll;

          VisitStreamConformance.AssertSameStream(
            TrustedBft(tree), GeneratedBft(tree), script, $"rootfixscan-bft {tree} [{strategy}]");
        }
    }
  }
}
