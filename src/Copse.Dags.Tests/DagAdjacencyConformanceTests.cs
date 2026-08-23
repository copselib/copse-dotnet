using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The adjacency-oracle battery (the tree family's BufferAdjacencyConformanceTests, dualized):
  // every walkable citizen's probes must agree with an oracle reconstructed from the VISIT
  // STREAM -- a genuinely different code path from the adjacency engines. The oracle reads the
  // sources, the out-edge groups in dispatch order, and the in-edge groups in InEdgeIndex order
  // (the grouped-arrival model's per-group order, a structural fact) off GetSources and GetEdges;
  // the walker must answer the same groups, same order, same payloads, over the whole corpus.
  [TestClass]
  public class DagAdjacencyConformanceTests
  {
    [TestMethod]
    public void EveryCitizen_AgreesWithTheStreamOracle()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
      {
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertAgreesWithOracle(walkable, factory(), $"{dagName}/{name}");

        AssertAgreesWithOracle(factory(), factory(), $"{dagName}/builder");
      }

      AssertAgreesWithOracle(new FamilyFreeDag(), DagWalkerCorpus.Diamond(), "diamond/foreign");
    }

    private static void AssertAgreesWithOracle<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, IDagnumerable<string, decimal> oracleSource, string label)
    {
      var edges = oracleSource.GetEdges().ToList();
      var outGroups = edges.GroupBy(edge => edge.Parent).ToDictionary(group => group.Key, group => group.Select(edge => $"{edge.Child}:{edge.Edge:0.00}").ToList());
      var inGroups = edges.GroupBy(edge => edge.Child).ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.InEdgeIndex).Select(edge => $"{edge.Parent}:{edge.Edge:0.00}").ToList());
      var sources = oracleSource.GetSources().ToList();

      var door = walkable.GetDagWalker();
      var walkedSources = new List<string>();
      for (var sourceIndex = 0; ; sourceIndex++)
      {
        var source = door.MoveToChild(sourceIndex);
        if (!source.HasValue)
          break;
        walkedSources.Add(source.Value.GetValue());
      }
      CollectionAssert.AreEqual(sources, walkedSources, $"the source group [{label}]");

      foreach (var row in walkable.GetHandlesWithValues())
      {
        var walker = walkable.GetDagWalkerAt(row.Handle);

        var outGroup = new List<string>();
        for (var index = 0; ; index++)
        {
          var step = walker.MoveToChild(index);
          if (!step.HasValue)
            break;
          outGroup.Add($"{step.Value.GetValue()}:{step.Edge:0.00}");
        }
        CollectionAssert.AreEqual(outGroups.TryGetValue(row.Value, out var expectedOut) ? expectedOut : new List<string>(), outGroup, $"out-edge group of {row.Value} [{label}]");

        var inGroup = new List<string>();
        for (var index = 0; ; index++)
        {
          var step = walker.MoveToParent(index);
          if (!step.HasValue || !step.Value.HasFocus)
            break;
          inGroup.Add($"{step.Value.GetValue()}:{step.Edge:0.00}");
        }
        CollectionAssert.AreEqual(inGroups.TryGetValue(row.Value, out var expectedIn) ? expectedIn : new List<string>(), inGroup, $"in-edge group of {row.Value} [{label}]");

        Assert.AreEqual(inGroup.Count == 0, sources.Contains(row.Value), $"sources are exactly the empty in-groups [{label}]");
      }
    }
  }
}
