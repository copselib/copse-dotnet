using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The codegen operator rollout beyond Where: the async PruneAfter (Copse.Linq.Async) is transcribed
  // by Copse.CodeGen into GeneratedPruneAfterTreenumerator, and that generated sync PruneAfter must
  // produce visit streams identical to the trusted public PruneAfter over the corpus -- pruning each
  // node in turn (exercising SkipDescendants at every depth) and under varied predicates.
  [TestClass]
  public class GeneratedPruneAfterConformanceTests
  {
    // Oracle: the trusted public PruneAfter over the engine.
    private static ITreenumerator<string> TrustedPruneAfter(string tree, Func<NodeContext<string>, bool> predicate)
      => EngineTree.Parse(tree).PruneAfter(predicate).GetDepthFirstTreenumerator();

    // The codegen'd sync PruneAfter over the same engine inner + predicate.
    private static ITreenumerator<string> GeneratedPruneAfter(string tree, Func<NodeContext<string>, bool> predicate)
      => new GeneratedPruneAfterTreenumerator<string>(
        () => EngineTree.Parse(tree).GetDepthFirstTreenumerator(),
        predicate);

    private static string[] DistinctLetters(string tree)
      => tree.Where(char.IsLetter).Select(c => c.ToString()).Distinct().ToArray();

    [TestMethod]
    public void PruneAtEachNode_TraverseAll_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var target in DistinctLetters(tree))
        {
          Func<NodeContext<string>, bool> predicate = nc => nc.Node == target; // prune after 'target'

          VisitStreamConformance.AssertSameStream(
            TrustedPruneAfter(tree, predicate),
            GeneratedPruneAfter(tree, predicate),
            VisitStreamConformance.TraverseAll,
            $"generated-pruneafter {tree} [prune '{target}']");
        }
    }

    [TestMethod]
    public void VariedPredicates_TraverseAll_MatchesTrusted()
    {
      var predicates = new List<(string desc, Func<NodeContext<string>, bool> predicate)>
      {
        ("prune-none", _ => false),
        ("prune-all", _ => true),
        ("prune-even-depth", nc => nc.Position.Depth % 2 == 0),
        ("prune-odd-depth", nc => nc.Position.Depth % 2 == 1),
      };

      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var (desc, predicate) in predicates)
          VisitStreamConformance.AssertSameStream(
            TrustedPruneAfter(tree, predicate),
            GeneratedPruneAfter(tree, predicate),
            VisitStreamConformance.TraverseAll,
            $"generated-pruneafter {tree} [{desc}]");
    }
  }
}
