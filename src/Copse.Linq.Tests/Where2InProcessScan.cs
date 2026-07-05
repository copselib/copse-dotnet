using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Fast in-process equivalent of Where2Test_BreadthFirst over the FULL exhaustive tree set
  // (Where2Tests.AllTreeStrings, groups c..i): runs the identical Where-wrapper-vs-materialized-
  // oracle BFT comparison per case, but in ONE test loop (no MSTest per-case overhead) and with
  // deserialization caching (the serializer is the slow part). This is why coverage lives here
  // and NOT in the [DynamicData] Where2Test_* methods: enumerating the full set during MSTest
  // discovery (even for [Ignore]d tests) overwhelms the host. Runs the whole set in seconds.
  [TestClass]
  public class Where2InProcessScan
  {
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void BreadthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.BreadthFirst);

    // DFT counterpart of BreadthFirstMatchesOracle. The DynamicData Where2Test_DepthFirst is
    // [Ignore]d (its ~891k-case discovery overwhelms the host) -- so before this, DFT Where had no
    // exhaustive oracle, only curated WhereTests. This closes that gap with the same in-process,
    // discovery-free loop, giving the DFT wrapper the same exhaustive safety net as the BFT wrapper.
    [TestMethod]
    public void DepthFirstMatchesOracle() => RunScan(TreeTraversalStrategy.DepthFirst);

    private void RunScan(TreeTraversalStrategy treeTraversalStrategy)
    {
      var deserializedByString = new Dictionary<string, ITreenumerable<string>>();
      ITreenumerable<string> Deserialize(string treeString)
      {
        if (!deserializedByString.TryGetValue(treeString, out var treenumerable))
        {
          treenumerable = TreeSerializer.DeserializeDepthFirstTree(treeString);
          deserializedByString[treeString] = treenumerable;
        }
        return treenumerable;
      }

      long total = 0;
      long failed = 0;
      var failures = new List<string>();

      foreach (var data in Where2Tests.GenerateCases(Where2Tests.AllTreeStrings))
      {
        total++;

        var treeString = (string)data[0];
        var expectedTreeString = (string)data[1];
        var skippedNodes = (string[])data[2];
        var composeOperations = (bool)data[3];
        var pairs = (Where2Tests.NodeAndTraversalStrategy[])data[4];

        NodeTraversalStrategies Selector(NodeContext<string> nodeContext)
        {
          foreach (var pair in pairs)
            if (pair.Node == nodeContext.Node)
              return pair.NodeTraversalStrategy;
          return NodeTraversalStrategies.TraverseAll;
        }

        ITreenumerable<string> sut;
        if (composeOperations)
        {
          sut = Deserialize(treeString);
          foreach (var node in skippedNodes)
          {
            var skipped = node;
            sut = sut.Where(nodeContext => nodeContext.Node != skipped);
          }
        }
        else
        {
          sut = Deserialize(treeString).Where(nodeContext => !skippedNodes.Contains(nodeContext.Node));
        }

        var expected = Key(Deserialize(expectedTreeString).GetTraversal(treeTraversalStrategy, Selector));
        // Take() bounds a hypothetical non-terminating wrapper regression into a length mismatch.
        var actual = Key(sut.GetTraversal(treeTraversalStrategy, Selector)).Take(100_000);

        if (!expected.SequenceEqual(actual))
        {
          failed++;
          if (failures.Count < 40)
            failures.Add(Where2Tests.GetTestDisplayName(null, data));
        }
      }

      TestContext.WriteLine($"Where2InProcessScan ({treeTraversalStrategy}): {total} cases across {Where2Tests.AllTreeStrings.Length} trees (groups c..i).");

      Assert.AreEqual(
        0L,
        failed,
        $"{treeTraversalStrategy} Where wrapper diverged from the oracle on {failed} of {total} cases:{Environment.NewLine}"
        + string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<(TreenumeratorMode, int, int, int, string)> Key(IEnumerable<NodeVisit<string>> visits) =>
      visits.Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node));
  }
}
