using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The flow doors: one type argument where the flat spellings need three -- and this file
  // compiling with exactly that is the pin (TResult / TDispatch appear only inside the lambda's
  // parameter types, so nothing but an explicit argument can fix them; the door fixes TNode and
  // TEdge from the receiver first). Each door equals its flat twin, content-exact.
  [TestClass]
  public class DagFlowDoorTests
  {
    [TestMethod]
    public void Scan_OneTypeArgument_EqualsTheFlatSpelling()
    {
      var dag = DagWalkerCorpus.Diamond();

      var viaDoor = dag.Sourcefix().Scan<decimal>((node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge));
      var viaFlat = dag.Sourcefix().Scan<decimal>((node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge));

      CollectionAssert.AreEqual(viaFlat.Values.Select(pairing => pairing.Accumulate).ToList(), viaDoor.Values.Select(pairing => pairing.Accumulate).ToList());
      Assert.AreEqual(0.54m, viaDoor.Values.Single(pairing => pairing.Node == "venture").Accumulate);

      var upward = dag.Sinkfix().Scan<int>((node, childResults) => 1 + childResults.Sum(result => result.Value));
      Assert.AreEqual(5, upward.Values.Single(pairing => pairing.Node == "apex").Accumulate, "the path-counted size, by the upward door");
    }

    [TestMethod]
    public void DispatchEdges_OneTypeArgument_EqualsTheFlatSpelling()
    {
      var dag = DagWalkerCorpus.Diamond();

      var viaDoor = dag.Sinkfix().DispatchEdges<decimal>((entity, arrivals, owners) =>
      {
        foreach (var owner in owners)
          owner.Dispatch(owner.Edge * 2);
      });
      var viaFlat = dag.Sinkfix().DispatchEdges<decimal>((entity, arrivals, owners) =>
      {
        foreach (var owner in owners)
          owner.Dispatch(owner.Edge * 2);
      });

      CollectionAssert.AreEqual(
        viaFlat.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge.Accumulate}").ToList(),
        viaDoor.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge.Accumulate}").ToList());
    }

    [TestMethod]
    public void Dispatch_SeededAndUnseeded_ThroughTheDoors()
    {
      var dag = DagWalkerCorpus.Diamond();

      // The virtual source family: the survey fires first with the seed arriving and the sources
      // as targets over the seed edge (default payload), so the boundary passes the seed through.
      var downward = dag.Sourcefix().Dispatch(1000m, (entity, arrivals, targets) =>
      {
        var holding = arrivals.Sum(arrival => arrival.Value);
        foreach (var target in targets)
          target.Dispatch(entity == null ? holding : holding * target.Edge);
      });
      Assert.AreEqual(540m, downward.Values.Single(result => result.Node == "venture").Arrivals.ToArray().Sum(), "the seed arrives at the apex; 1000 × 54% reaches the venture");

      var upward = dag.Sinkfix().Dispatch<decimal>((entity, arrivals, owners) =>
      {
        var holding = entity == "venture" ? 1000m : arrivals.Sum(arrival => arrival.Value);
        foreach (var owner in owners)
          owner.Dispatch(holding * owner.Edge);
      });
      Assert.AreEqual(540m, upward.Values.Single(result => result.Node == "apex").Arrivals.ToArray().Sum(), "the venture's 1000 attributed upward to the apex: 540");
    }

    [TestMethod]
    public void ReturnShapedFolds_EqualTheirSlotSpellings_AndCountIsChecked()
    {
      var dag = DagWalkerCorpus.Diamond();

      // The reallocation-shaped fold, both spellings.
      var viaSlots = dag.Sinkfix().DispatchEdges<decimal>((entity, arrivals, owners) =>
      {
        foreach (var owner in owners)
          owner.Dispatch(owner.Edge * 2);
      });
      var viaReturn = dag.Sinkfix().DispatchEdges<decimal>((arrivals, entity, owners) => owners.Select(owner => owner.Edge * 2).ToList());

      CollectionAssert.AreEqual(
        viaSlots.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge.Accumulate}").ToList(),
        viaReturn.GetEdges().Select(edge => $"{edge.Parent}->{edge.Child}:{edge.Edge.Accumulate}").ToList());

      // The seeded money fold, return-shaped: the boundary passes the seed through.
      var downward = dag.Sourcefix().Dispatch(1000m, (arrivals, entity, targets) =>
      {
        var holding = arrivals.ToArray().Sum(arrival => arrival.Value);
        return targets.Select(target => entity == null ? holding : holding * target.Edge).ToList();
      });
      Assert.AreEqual(540m, downward.Values.Single(result => result.Node == "venture").Arrivals.ToArray().Sum());

      // One value per target, or the seats refuse.
      StringAssert.Contains(
        Assert.ThrowsException<InvalidOperationException>(() =>
          dag.Sinkfix().DispatchEdges<decimal>((arrivals, entity, owners) => new List<decimal>())).Message,
        "one per target");
    }
  }
}
