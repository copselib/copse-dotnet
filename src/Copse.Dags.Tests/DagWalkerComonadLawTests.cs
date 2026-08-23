using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The Store comonad's three laws against the real Extend (the tree family's
  // WalkerComonadLawTests, dualized), over every citizen, plus the two coherence pins that
  // make the scan tier's cross-tenancy an equation: SourcefixScan ≡ Extend(the all-in-paths
  // fold) and SinkfixScan ≡ Extend(the all-out-paths fold). The folds' reference schedule is
  // naive per-node observation -- PATH-priced on a dag (the diamond's venture is reached twice
  // and counts twice, by design: the scan's per-edge roll-up double count, pinned 5 over 4
  // nodes) -- and the scan is the O(V+E) schedule the fold's semiring shape admits. Same
  // answer, two prices: the foundation's class restriction, as a test.
  [TestClass]
  public class DagWalkerComonadLawTests
  {
    private static Dictionary<string, TValue> ByName<THandle, TValue>(IWalkableDagnumerable<TValue, THandle, decimal> walkable, IWalkableDagnumerable<string, THandle, decimal> names)
      => walkable.GetHandles().ToDictionary(handle => names.GetDagWalkerAt(handle).GetValue(), handle => walkable.GetDagWalkerAt(handle).GetValue());

    [TestMethod]
    public void ComonadLaw_ExtendOfExtract_IsIdentity()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          AssertExtendOfExtract(walkable, $"{dagName}/{name}");

      AssertExtendOfExtract(DagWalkerCorpus.SharedLeaf(), "sharedLeaf/builder");
      AssertExtendOfExtract(new FamilyFreeDag(), "diamond/foreign");
    }

    private static void AssertExtendOfExtract<THandle>(IWalkableDagnumerable<string, THandle, decimal> walkable, string label)
    {
      var extended = walkable.Extend((topology, handle) => topology.GetValue(handle));
      Assert.AreEqual(DagWalkerCorpus.Content(walkable), DagWalkerCorpus.Content(extended), $"extend(extract) streams the source [{label}]");
      foreach (var handle in walkable.GetHandles())
        Assert.AreEqual(walkable.GetDagWalkerAt(handle).GetValue(), extended.GetDagWalkerAt(handle).GetValue(), $"extend(extract) labels the source [{label}]");
    }

    [TestMethod]
    public void ComonadLaw_ExtractAfterExtend_RecoversTheObserver()
    {
      // The observer is genuinely neighborhood-dependent: in-degree and out-degree at the focus.
      Func<IDagTopology<string, int, decimal>, int, string> observer = (topology, handle) =>
      {
        var inDegree = 0;
        while (topology.TryGetParentAt(handle, inDegree).HasValue)
          inDegree++;
        var outDegree = 0;
        while (topology.TryGetChildAt(handle, outDegree).HasValue)
          outDegree++;
        return $"{topology.GetValue(handle)}:{inDegree}/{outDegree}";
      };

      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var extended = walkable.Extend(observer);
          var topology = walkable.GetDagWalker().Topology;
          foreach (var handle in walkable.GetHandles())
            Assert.AreEqual(observer(topology, handle), extended.GetDagWalkerAt(handle).GetValue(), $"{dagName}/{name}");
        }

      var diamond = DagWalkerCorpus.Diamond().Materialize();
      var labels = ByName(diamond.Extend(observer), diamond);
      Assert.AreEqual("venture:2/0", labels["venture"]);
      Assert.AreEqual("apex:0/2", labels["apex"]);
    }

    [TestMethod]
    public void ComonadLaw_CoAssociativity()
    {
      // g: value plus in-degree; f: an observation of the g-extended dag consulting the first
      // parent's g-label -- real co-Kleisli composition, exercised on shared nodes.
      Func<IDagTopology<string, int, decimal>, int, string> g = (topology, handle) =>
      {
        var inDegree = 0;
        while (topology.TryGetParentAt(handle, inDegree).HasValue)
          inDegree++;
        return $"{topology.GetValue(handle)}@{inDegree}";
      };

      Func<IDagTopology<string, int, decimal>, int, string> f = (topology, handle) =>
      {
        var firstParent = topology.TryGetParentAt(handle, 0);
        var parentLabel = firstParent.HasValue ? topology.GetValue(firstParent.Handle) : "⊤";
        return $"{topology.GetValue(handle)}<{parentLabel}";
      };

      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var stepwise = walkable.Extend(g).Extend(f);
          var gTopology = walkable.Extend(g).GetDagWalker().Topology;
          var composed = walkable.Extend((topology, handle) => f(gTopology, handle));

          Assert.AreEqual(DagWalkerCorpus.Content(composed), DagWalkerCorpus.Content(stepwise), $"co-associativity streams [{dagName}/{name}]");
          foreach (var handle in walkable.GetHandles())
            Assert.AreEqual(composed.GetDagWalkerAt(handle).GetValue(), stepwise.GetDagWalkerAt(handle).GetValue(), $"co-associativity values [{dagName}/{name}]");
        }

      var diamond = DagWalkerCorpus.Diamond().Materialize();
      Assert.AreEqual("venture@2<left@1", ByName(diamond.Extend(g).Extend(f), diamond)["venture"], "the pinned composite at the shared node");
    }

    [TestMethod]
    public void Coherence_SourcefixScan_IsExtendOfTheInPathsFold()
    {
      // Ownership lookthrough: a source owns itself wholly; every other node is owned to the
      // extent its parents are, through each in-edge -- the sum over all in-paths of the
      // products along them. The extend observer climbs every path (exponential in general);
      // the scan does it in one topological pass.
      foreach (var factory in new Func<Dag<string, decimal>>[] { DagWalkerCorpus.Diamond, DagWalkerCorpus.SharedLeaf, DagWalkerCorpus.Chain })
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var viaScan = factory().Sourcefix().Scan<decimal>(
            (node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge))
            .GetTopologicalOrder().ToDictionary(result => result.Node, result => result.Accumulate);

          var viaExtend = ByName(walkable.Extend((topology, handle) => Lookthrough(topology, handle)), walkable);

          CollectionAssert.AreEquivalent(viaScan.Keys.ToList(), viaExtend.Keys.ToList(), name);
          foreach (var pair in viaScan)
            Assert.AreEqual(pair.Value, viaExtend[pair.Key], $"scan ≡ extend(in-paths fold) at {pair.Key} [{name}]");
        }

      Assert.AreEqual(0.54m, Lookthrough(DagWalkerCorpus.Diamond().Materialize().GetDagWalker().Topology, 3), "the diamond's lookthrough, by climbing");
    }

    private static decimal Lookthrough(IDagTopology<string, int, decimal> topology, int handle)
    {
      var first = topology.TryGetParentAt(handle, 0);
      if (!first.HasValue)
        return 1m;

      var owned = 0m;
      for (var step = first; step.HasValue; step = topology.TryGetParentAt(handle, step.EdgeIndex + 1))
        owned += Lookthrough(topology, step.Handle) * step.Edge;
      return owned;
    }

    [TestMethod]
    public void Coherence_SinkfixScan_IsExtendOfTheOutPathsFold()
    {
      // Path-counted size: one for the node plus the sizes below each out-edge -- a shared node
      // counts once per path that reaches it (the scan's per-edge roll-up, the caller's
      // documented choice; the diamond reads 5 over 4 nodes at the apex).
      foreach (var factory in new Func<Dag<string, decimal>>[] { DagWalkerCorpus.Diamond, DagWalkerCorpus.SharedLeaf, DagWalkerCorpus.TwoIslands })
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var viaScan = factory().Sinkfix().Scan<int>((node, inflows) => 1 + inflows.Sum(inflow => inflow.Value))
            .GetTopologicalOrder().ToDictionary(result => result.Node, result => result.Accumulate);

          var viaExtend = ByName(walkable.Extend((topology, handle) => PathCountedSize(topology, handle)), walkable);

          foreach (var pair in viaScan)
            Assert.AreEqual(pair.Value, viaExtend[pair.Key], $"scan ≡ extend(out-paths fold) at {pair.Key} [{name}]");
        }

      Assert.AreEqual(5, ByName(DagWalkerCorpus.Diamond().Materialize().Extend((topology, handle) => PathCountedSize(topology, handle)), DagWalkerCorpus.Diamond().Materialize())["apex"], "5 over 4 nodes: the deliberate double count");
    }

    private static int PathCountedSize(IDagTopology<string, int, decimal> topology, int handle)
    {
      var size = 1;
      for (var step = topology.TryGetChildAt(handle, 0); step.HasValue; step = topology.TryGetChildAt(handle, step.EdgeIndex + 1))
        size += PathCountedSize(topology, step.Handle);
      return size;
    }
  }
}
