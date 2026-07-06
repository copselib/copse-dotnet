using Copse.Core;
using Copse.TestUtils;
using Copse.Treenumerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // Conformance for the shared-cadence DFT driver (Option-B prototype): its visit stream must be
  // identical to the engine's over the corpus AND the full per-node strategy matrix. Built over the
  // SAME flat pre-order source the oracle uses (EngineTree.ParseArrays + PreorderChildEnumerator), so
  // any divergence is the cadence's, not the source's. Thin adapter over VisitStreamConformance.
  [TestClass]
  public class CadenceDepthFirstConformanceTests
  {
    private static ITreenumerator<string> CadenceDft(string tree)
    {
      var (values, sizes) = EngineTree.ParseArrays(tree);
      return new DepthFirstCadenceTreenumerator<string, int, PreorderChildEnumerator>(
        RootIndices(sizes),
        nc => new PreorderChildEnumerator(sizes, nc.Node),
        i => values[i]);
    }

    // Roots are the top-level pre-order spans: index 0, then hop by each root's subtree size.
    private static IEnumerable<int> RootIndices(int[] subtreeSizes)
    {
      for (int i = 0; i < subtreeSizes.Length; i += subtreeSizes[i])
        yield return i;
    }

    [TestMethod]
    public void TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(CadenceDft, depthFirst: true, "cadence-dft");

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(CadenceDft, depthFirst: true, "cadence-dft");

    [TestMethod]
    public void StrategiesOnVisitingVisitsAreIgnored()
    {
      NodeTraversalStrategies Script(TreenumeratorMode mode, string node, int visitCount)
        => mode == TreenumeratorMode.VisitingNode
          ? VisitStreamConformance.SchedulingStrategies[(node[0] + visitCount) % VisitStreamConformance.SchedulingStrategies.Length]
          : NodeTraversalStrategies.TraverseAll;

      foreach (var tree in VisitStreamConformance.TreeCorpus)
        if (tree.Length > 0)
          VisitStreamConformance.AssertSameStream(
            VisitStreamConformance.Engine(tree, depthFirst: true), CadenceDft(tree), Script, $"cadence-dft {tree}");
    }

    [TestMethod]
    public void PreEnumerationStateIsForestRoot()
      => VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(CadenceDft("a(b,c)"), "cadence-dft");
  }
}
