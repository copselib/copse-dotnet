using Copse.Stores;
using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Linq.Tests
{
  // Conformance for the flat family's random-access tier (PreorderTreenumerable /
  // LevelOrderTreenumerable) and, through them, all four store treenumerators, over COMPLETED
  // array-backed stores -- independent of the memoize machinery that also rides them. Thin
  // adapter over VisitStreamConformance (the shared visit-contract battery).
  [TestClass]
  public class FlatFamilyConformanceTests
  {
    // The stores under test are the PUBLIC completed array stores (Copse.Stores), built by the
    // PUBLIC capture factories: the whole chain under conformance -- capture loop, store,
    // decoder -- is product code (the hand-rolled build loops these replaced were line-for-line
    // copies of the factories; hygiene item E, STORE_FAMILY_REVIEW.md). Any bug anywhere in the
    // chain diffs against the engine oracle, which is built independently.

    private static ITreenumerable<string> Preorder(string tree)
      => new PreorderTreenumerable<string, PreorderArrayStore<string>>(
           PreorderCapture.CaptureFrom(TreeSerializer.DeserializeDepthFirstTree(tree)));

    private static ITreenumerable<string> LevelOrder(string tree)
      => new LevelOrderTreenumerable<string, LevelOrderArrayStore<string>>(
           LevelOrderCapture.CaptureFrom(TreeSerializer.DeserializeDepthFirstTree(tree)));

    // ---------------------------------------------------------------------------------------
    // Conformance: both dimensions of both wrappers, TraverseAll and the full strategy matrix.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Preorder_TraverseAll_MatchesEngine()
    {
      VisitStreamConformance.AssertTraverseAllConforms(tree => Preorder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder");
      VisitStreamConformance.AssertTraverseAllConforms(tree => Preorder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "preorder");
    }

    [TestMethod]
    public void LevelOrder_TraverseAll_MatchesEngine()
    {
      VisitStreamConformance.AssertTraverseAllConforms(tree => LevelOrder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "levelorder");
      VisitStreamConformance.AssertTraverseAllConforms(tree => LevelOrder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder");
    }

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => Preorder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "preorder");

    [TestMethod]
    public void Preorder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => Preorder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "preorder");

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_DepthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => LevelOrder(tree).GetDepthFirstTreenumerator(), depthFirst: true, "levelorder");

    [TestMethod]
    public void LevelOrder_EveryNodeEveryStrategy_MatchesEngine_BreadthFirst()
      => VisitStreamConformance.AssertStrategyMatrixConforms(tree => LevelOrder(tree).GetBreadthFirstTreenumerator(), depthFirst: false, "levelorder");

    [TestMethod]
    public void PreEnumerationStateIsTheForestRoot()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
      {
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(Preorder(tree).GetDepthFirstTreenumerator(), $"preorder '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(Preorder(tree).GetBreadthFirstTreenumerator(), $"preorder '{tree}' BFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(LevelOrder(tree).GetDepthFirstTreenumerator(), $"levelorder '{tree}' DFT");
        VisitStreamConformance.AssertPreEnumerationStateIsTheForestRoot(LevelOrder(tree).GetBreadthFirstTreenumerator(), $"levelorder '{tree}' BFT");
      }
    }
  }
}
