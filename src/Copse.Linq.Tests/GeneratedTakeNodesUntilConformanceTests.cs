using Copse.Core;
using Copse.Linq.Generated;
using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Codegen operator rollout: the async TakeNodesUntil (Copse.Linq.Async) is transcribed into
  // GeneratedTakeNodesUntilTreenumerator, which must produce visit streams identical to the trusted
  // public TakeNodesUntil over the corpus -- stopping at each node in turn, both keeping and dropping
  // the final matched node (the two scheduling-stopped branches), plus varied predicates.
  [TestClass]
  public class GeneratedTakeNodesUntilConformanceTests
  {
    private static ITreenumerator<string> Trusted(string tree, Func<NodeContext<string>, bool> predicate, bool keepFinal)
      => EngineTree.Parse(tree).TakeNodesUntil(predicate, keepFinal).GetDepthFirstTreenumerator();

    private static ITreenumerator<string> Generated(string tree, Func<NodeContext<string>, bool> predicate, bool keepFinal)
      => new GeneratedTakeNodesUntilTreenumerator<string>(
        () => EngineTree.Parse(tree).GetDepthFirstTreenumerator(),
        predicate,
        keepFinal);

    private static string[] DistinctLetters(string tree)
      => tree.Where(char.IsLetter).Select(c => c.ToString()).Distinct().ToArray();

    [TestMethod]
    public void StopAtEachNode_KeepAndDrop_MatchesTrusted()
    {
      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var target in DistinctLetters(tree))
          foreach (var keepFinal in new[] { true, false })
          {
            Func<NodeContext<string>, bool> predicate = nc => nc.Node == target;

            VisitStreamConformance.AssertSameStream(
              Trusted(tree, predicate, keepFinal),
              Generated(tree, predicate, keepFinal),
              VisitStreamConformance.TraverseAll,
              $"generated-takeuntil {tree} [until '{target}', keepFinal={keepFinal}]");
          }
    }

    [TestMethod]
    public void VariedPredicates_KeepAndDrop_MatchesTrusted()
    {
      var predicates = new List<(string desc, Func<NodeContext<string>, bool> predicate)>
      {
        ("never", _ => false),
        ("always", _ => true),
        ("depth>=1", nc => nc.Position.Depth >= 1),
        ("odd-sibling", nc => nc.Position.SiblingIndex % 2 == 1),
      };

      foreach (var tree in VisitStreamConformance.TreeCorpus)
        foreach (var (desc, predicate) in predicates)
          foreach (var keepFinal in new[] { true, false })
            VisitStreamConformance.AssertSameStream(
              Trusted(tree, predicate, keepFinal),
              Generated(tree, predicate, keepFinal),
              VisitStreamConformance.TraverseAll,
              $"generated-takeuntil {tree} [{desc}, keepFinal={keepFinal}]");
    }
  }
}
