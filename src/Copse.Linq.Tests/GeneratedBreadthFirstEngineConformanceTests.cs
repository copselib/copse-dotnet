using Copse.Core;
using Copse.Generated;
using Copse.TestUtils;
using Copse.Treenumerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // Corpus conformance for the shared BreadthFirstPathState, via the codegen'd sync BFS engine twin
  // (GeneratedBreadthFirstTreenumerator, transcribed from AsyncBreadthFirstTreenumerator). Its visit
  // stream must match the engine oracle's breadth-first stream over the corpus AND the full per-node
  // strategy matrix -- the rigorous validation the async BFS engine's one-tree smoke test lacked.
  [TestClass]
  public class GeneratedBreadthFirstEngineConformanceTests
  {
    private static ITreenumerator<string> GeneratedBft(string tree)
    {
      var (values, sizes) = EngineTree.ParseArrays(tree);
      return new GeneratedBreadthFirstTreenumerator<string, int, PreorderChildCursor>(
        RootIndices(sizes),
        nc => new PreorderChildCursor(sizes, nc.Node),
        i => values[i]);
    }

    private static IEnumerable<int> RootIndices(int[] subtreeSizes)
    {
      for (int i = 0; i < subtreeSizes.Length; i += subtreeSizes[i])
        yield return i;
    }

    [TestMethod]
    public void TraverseAll_MatchesEngine()
      => VisitStreamConformance.AssertTraverseAllConforms(GeneratedBft, depthFirst: false, "generated-bfs-engine");

    [TestMethod]
    public void EveryNodeEveryStrategy_MatchesEngine()
      => VisitStreamConformance.AssertStrategyMatrixConforms(GeneratedBft, depthFirst: false, "generated-bfs-engine");

    [TestMethod]
    public void PreEnumerationStateIsForestRoot()
      => VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(GeneratedBft("a(b,c)"), "generated-bfs-engine");
  }
}
