using Copse.Core;
using Copse.Traversal;
using Copse.Generated;
using Copse.TestUtils;
using Copse.Treenumerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // Conformance for the shared-path DFT drivers built on DepthFirstPathState: the hand-written
  // direct driver and the codegen'd sync twin must each produce visit streams identical to the
  // engine's over the corpus AND the full per-node strategy matrix. Built over the SAME flat
  // pre-order source the oracle uses (EngineTree.ParseArrays), so any divergence is the driver's,
  // not the source's. Thin adapter over VisitStreamConformance.
  [TestClass]
  public class DepthFirstDriverConformanceTests
  {
    // Roots are the top-level pre-order spans: index 0, then hop by each root's subtree size.
    private static IEnumerable<int> RootIndices(int[] subtreeSizes)
    {
      for (int i = 0; i < subtreeSizes.Length; i += subtreeSizes[i])
        yield return i;
    }

    // --- The hand-written direct-style driver (the codegen twin's shape). ---

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

    [TestMethod]
    public void Direct_PreEnumerationStateIsForestRoot()
      => VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(DirectDft("a(b,c)"), "direct-dft");

    // --- The CODEGEN'D sync twin (Copse.CodeGen async->sync transcription of the async driver).
    //     This is the spike's payoff: generated code passes the same battery. ---

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
