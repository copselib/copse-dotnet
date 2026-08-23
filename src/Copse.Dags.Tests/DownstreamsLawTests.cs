using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The cofree duplicate's laws, nodewise (the tree family's SubtreesLawTests, dualized):
  // Downstreams relabels every node with its downstream cone. Counit: the cone at a single
  // source is the whole dag, and every label's source value is the original value. Severance:
  // exactly at the cone's boundary -- inside the cone, sharing is KEPT (the diamond's venture
  // has two parents in apex's cone) and outside in-edges are GONE (one parent in left's cone).
  // Co-associativity: a cone of a cone is the deeper cone. The outer shape is the source
  // shape. The reverse door: a walker's Downstream() is duplicate's label. Interior labels are
  // pinned by hand from the drawing, so the counits cannot be green by mutual bug.
  [TestClass]
  public class DownstreamsLawTests
  {
    private static Dictionary<string, IWalkableDagnumerable<string, int, decimal>> ConesByName(IWalkableDagnumerable<string, int, decimal> walkable)
      => walkable.Downstreams().GetHandles().ToDictionary(
        handle => walkable.GetDagWalkerAt(handle).GetValue(),
        handle => walkable.Downstreams().GetDagWalkerAt(handle).GetValue());

    [TestMethod]
    public void Counit_TheConeAtTheSingleSource_IsTheWholeDag()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
        Assert.AreEqual(DagWalkerCorpus.Content(walkable), DagWalkerCorpus.Content(ConesByName(walkable)["apex"]), name);
    }

    [TestMethod]
    public void Counit_EveryLabelsSourceValue_IsTheOriginalValue()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          foreach (var handle in walkable.GetHandles())
          {
            var cone = walkable.Downstreams().GetDagWalkerAt(handle).GetValue();
            Assert.AreEqual(walkable.GetDagWalkerAt(handle).GetValue(), cone.TryGetDagWalkerAtSourceIndex(0).Value.GetValue(), $"{dagName}/{name}");
            Assert.IsFalse(cone.TryGetDagWalkerAtSourceIndex(1).HasValue, $"a cone has one source [{dagName}/{name}]");
          }
    }

    [TestMethod]
    public void Severance_IsExactlyAtTheConesBoundary_SharingInsideIsKept()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var cones = ConesByName(walkable);

        // left's cone: left -> venture, and venture has ONE parent here -- right is outside.
        var leftCone = cones["left"];
        CollectionAssert.AreEqual(new[] { "left->venture:0.70" }, DagWalkerCorpus.Edges(leftCone), name);
        var ventureInLeftCone = leftCone.GetDagWalkerAt(leftCone.GetHandlesWithValues().Single(row => row.Value == "venture").Handle);
        Assert.AreEqual("left", ventureInLeftCone.MoveToParent(0).Value.GetValue(), $"the surviving in-edge [{name}]");
        Assert.IsFalse(ventureInLeftCone.MoveToParent(1).HasValue, $"the outside in-edge is severed [{name}]");
        var leftInCone = leftCone.TryGetDagWalkerAtSourceIndex(0).Value;
        Assert.IsTrue(leftInCone.MoveToParent(0).HasValue && !leftInCone.MoveToParent(0).Value.HasFocus, $"the cone's root steps up to the cone's own unfocused stance [{name}]");

        // apex's cone: venture keeps BOTH parents -- sharing inside the cone is representable, kept.
        var apexCone = cones["apex"];
        var ventureInApexCone = apexCone.GetDagWalkerAt(apexCone.GetHandlesWithValues().Single(row => row.Value == "venture").Handle);
        Assert.AreEqual("left", ventureInApexCone.MoveToParent(0).Value.GetValue(), name);
        Assert.AreEqual("right", ventureInApexCone.MoveToParent(1).Value.GetValue(), name);

        // venture's cone: the node alone.
        Assert.AreEqual("nodes[venture] edges[] sources[venture]", DagWalkerCorpus.Content(cones["venture"]), name);
      }
    }

    [TestMethod]
    public void CoAssociativity_AConeOfACone_IsTheDeeperCone()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
      {
        var alphaCone = ConesByName(walkable)["alpha"];
        var middleInAlphaCone = alphaCone.GetHandlesWithValues().Single(row => row.Value == "middle").Handle;
        var viaTwoSteps = alphaCone.Downstreams().GetDagWalkerAt(middleInAlphaCone).GetValue();
        var direct = ConesByName(walkable)["middle"];
        Assert.AreEqual(DagWalkerCorpus.Content(direct), DagWalkerCorpus.Content(viaTwoSteps), name);
        Assert.AreEqual("nodes[middle,sharedLeaf] edges[middle->sharedLeaf:0.30] sources[middle]", DagWalkerCorpus.Content(direct), name);
      }
    }

    [TestMethod]
    public void TheOuterShape_IsTheSourceShape()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          var outer = walkable.Downstreams().Extend((topology, handle) => topology.GetValue(handle).TryGetDagWalkerAtSourceIndex(0).Value.GetValue());
          Assert.AreEqual(DagWalkerCorpus.Content(walkable), DagWalkerCorpus.Content(outer), $"{dagName}/{name}");
        }
    }

    [TestMethod]
    public void TheReverseDoor_AWalkersDownstream_IsDuplicatesLabel()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(
            DagWalkerCorpus.Content(walkable.Downstreams().GetDagWalkerAt(handle).GetValue()),
            DagWalkerCorpus.Content(walkable.GetDagWalkerAt(handle).Downstream()),
            $"{name} @ {walkable.GetDagWalkerAt(handle).GetValue()}");
    }

    [TestMethod]
    public void InteriorCones_PinnedByHand()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
      {
        var cones = ConesByName(walkable);
        Assert.AreEqual("nodes[alpha,middle,sharedLeaf] edges[alpha->middle:0.50,alpha->sharedLeaf:0.10,middle->sharedLeaf:0.30] sources[alpha]", DagWalkerCorpus.Content(cones["alpha"]), name);
        Assert.AreEqual("nodes[beta,sharedLeaf] edges[beta->sharedLeaf:0.20] sources[beta]", DagWalkerCorpus.Content(cones["beta"]), name);
        Assert.AreEqual("nodes[sharedLeaf] edges[] sources[sharedLeaf]", DagWalkerCorpus.Content(cones["sharedLeaf"]), name);
      }
    }
  }
}
