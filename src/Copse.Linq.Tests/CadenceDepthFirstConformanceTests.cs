using Copse.Core;
using Copse.Engine;
using Copse.Generated;
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

    // --- The direct-style driver (codegen twin's shape) must also conform. ---

    private static ITreenumerator<string> DirectDft(string tree)
    {
      var (values, sizes) = EngineTree.ParseArrays(tree);
      return new DepthFirstDirectTreenumerator<string, int, PreorderChildEnumerator>(
        RootIndices(sizes),
        nc => new PreorderChildEnumerator(sizes, nc.Node),
        i => values[i]);
    }

    [TestMethod]
    public void Direct_TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(DirectDft, depthFirst: true, "direct-dft");

    [TestMethod]
    public void Direct_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(DirectDft, depthFirst: true, "direct-dft");

    // --- The CODEGEN'D sync twin (Copse.CodeGen async->sync transcription of the async driver) must
    //     also conform. This is the spike's payoff: generated code passes the same battery. ---

    private static ITreenumerator<string> GeneratedDft(string tree)
    {
      var (values, sizes) = EngineTree.ParseArrays(tree);
      return new GeneratedDepthFirstTreenumerator<string, int, ForwardPreorderChildEnumerator>(
        RootIndices(sizes),
        nc => new ForwardPreorderChildEnumerator(sizes, nc.Node),
        i => values[i]);
    }

    [TestMethod]
    public void Generated_TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(GeneratedDft, depthFirst: true, "generated-dft");

    [TestMethod]
    public void Generated_EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(GeneratedDft, depthFirst: true, "generated-dft");

    // Current-style adapter over the out-style PreorderChildEnumerator, so the generated driver (which
    // pulls via MoveNext()+Current) can run over the same flat source the oracle uses.
    private struct ForwardPreorderChildEnumerator : IForwardChildEnumerator<int>
    {
      private PreorderChildEnumerator _inner;
      private NodeAndSiblingIndex<int> _current;

      public ForwardPreorderChildEnumerator(int[] subtreeSizes, int parentIndex)
      {
        _inner = new PreorderChildEnumerator(subtreeSizes, parentIndex);
        _current = default;
      }

      public bool MoveNext()
      {
        if (_inner.MoveNext(out var child))
        {
          _current = child;
          return true;
        }
        return false;
      }

      public NodeAndSiblingIndex<int> Current => _current;

      public void Dispose() => _inner.Dispose();
    }
  }
}
