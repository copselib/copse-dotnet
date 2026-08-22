using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The lenses' oracle equivalences -- each lens vs its streaming twin, the pair-citizen rule as
  // tests (the DAG's naturality squares: region-restricted view ≡ prune-the-complement sweep):
  // the PruneAfter lens vs the streaming PruneAfter (both halves), Downstream() vs
  // TakeDownstreamWhere at one node, Upstream() vs TakeUpstreamWhere, the walker Transpose vs the
  // buffer Transpose, the conjugate law Upstream = Transpose ∘ Downstream ∘ Transpose, and lenses
  // stacking without a lattice.
  [TestClass]
  public class DagLensTests
  {
    [TestMethod]
    public void PruneAfterLens_OrderHalf_MatchesTheStreamingOracle()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
        Assert.AreEqual(
          DagWalkerCorpus.Content(((IDagnumerable<string, decimal>)walkable).PruneAfter(node => node == "left")),
          DagWalkerCorpus.Content(walkable.PruneAfter(node => node == "left")),
          name);

      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
        Assert.AreEqual(
          DagWalkerCorpus.Content(((IDagnumerable<string, decimal>)walkable).PruneAfter(node => node == "alpha")),
          DagWalkerCorpus.Content(walkable.PruneAfter(node => node == "alpha")),
          name);
    }

    [TestMethod]
    public void PruneAfterLens_AdjacencyHalf_ShedsOutEdgesAtMatches_AndTheirInEdgesAtTargets()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var pruned = walkable.PruneAfter(node => node == "left");
        var rows = pruned.GetHandlesWithValues().ToDictionary(row => row.Value, row => row.Handle);

        Assert.IsFalse(pruned.GetDagWalkerAt(rows["left"]).MoveToChild(0).HasValue, $"a matched node hands out no out-edges [{name}]");
        var venture = pruned.GetDagWalkerAt(rows["venture"]);
        Assert.AreEqual("right", venture.MoveToParent(0).Value.GetValue(), $"the surviving in-edge [{name}]");
        Assert.AreEqual(0.30m, venture.MoveToParent(0).Edge, name);
        Assert.IsFalse(venture.MoveToParent(1).HasValue, $"the pruned parent's edge is gone from the in-edge group [{name}]");
        Assert.AreEqual("right", pruned.GetDagWalkerAt(rows["apex"]).MoveToChild(1).Value.GetValue(), $"unmatched nodes keep their groups [{name}]");
        Assert.AreEqual("nodes[apex,left,right,venture] edges[apex->left:0.60,apex->right:0.40,right->venture:0.30] sources[apex]", DagWalkerCorpus.Content(pruned), name);
      }
    }

    [TestMethod]
    public void PruneAfterLens_StacksWithoutALattice()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.Diamond))
      {
        var stacked = walkable.PruneAfter(node => node == "left").PruneAfter(node => node == "right");
        var streamed = ((IDagnumerable<string, decimal>)walkable).PruneAfter(node => node == "left").PruneAfter(node => node == "right");
        Assert.AreEqual(DagWalkerCorpus.Content(streamed), DagWalkerCorpus.Content(stacked), name);
        Assert.AreEqual("nodes[apex,left,right] edges[apex->left:0.60,apex->right:0.40] sources[apex]", DagWalkerCorpus.Content(stacked), $"the venture starves [{name}]");
        Assert.AreEqual(0, stacked.GetHandlesWithValues().Count(row => row.Value == "venture"), $"the starved node is handed out by no probe [{name}]");
      }
    }

    [TestMethod]
    public void Downstream_IsTakeDownstreamWhere_AtOneNode()
    {
      foreach (var factory in new System.Func<Dag<string, decimal>>[] { DagWalkerCorpus.Diamond, DagWalkerCorpus.SharedLeaf })
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          foreach (var row in walkable.GetHandlesWithValues())
            Assert.AreEqual(
              DagWalkerCorpus.Content(factory().TakeDownstreamWhere(node => node == row.Value)),
              DagWalkerCorpus.Content(walkable.GetDagWalkerAt(row.Handle).Downstream()),
              $"{name} @ {row.Value}");
    }

    [TestMethod]
    public void Upstream_IsTakeUpstreamWhere_AtOneNode()
    {
      foreach (var factory in new System.Func<Dag<string, decimal>>[] { DagWalkerCorpus.Diamond, DagWalkerCorpus.SharedLeaf })
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
          foreach (var row in walkable.GetHandlesWithValues())
            Assert.AreEqual(
              DagWalkerCorpus.Content(factory().TakeUpstreamWhere(node => node == row.Value)),
              DagWalkerCorpus.Content(walkable.GetDagWalkerAt(row.Handle).Upstream()),
              $"{name} @ {row.Value}");

      var diamond = DagWalkerCorpus.Diamond().Materialize();
      var left = diamond.GetDagWalkerAt(diamond.GetHandlesWithValues().Single(row => row.Value == "left").Handle);
      Assert.AreEqual("nodes[apex,left] edges[apex->left:0.60] sources[apex]", DagWalkerCorpus.Content(left.Upstream()), "the upstream cone keeps the original orientation");
    }

    [TestMethod]
    public void WalkerTranspose_IsTheBufferTranspose_AndUpstreamIsTheConjugate()
    {
      foreach (var (dagName, factory) in DagWalkerCorpus.All())
        foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(factory))
        {
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().Transpose()),
            DagWalkerCorpus.Content(walkable.GetDagWalker().Transpose().Downstream()),
            $"the free lens ≡ the materialized reversal [{dagName}/{name}]");

          foreach (var handle in walkable.GetHandles())
            Assert.AreEqual(
              DagWalkerCorpus.Content(walkable.GetDagWalkerAt(handle).Upstream()),
              DagWalkerCorpus.Content(walkable.GetDagWalkerAt(handle).Transpose().Downstream().Transpose()),
              $"Upstream = Transpose ∘ Downstream ∘ Transpose [{dagName}/{name}]");
        }
    }

    [TestMethod]
    public void LensesStack_AConeOfAPrunedView()
    {
      foreach (var (name, walkable) in DagWalkerLawProviders.IntHandled(DagWalkerCorpus.SharedLeaf))
      {
        var pruned = walkable.PruneAfter(node => node == "middle");
        var alpha = pruned.GetDagWalkerAt(pruned.GetHandlesWithValues().Single(row => row.Value == "alpha").Handle);
        Assert.AreEqual(
          DagWalkerCorpus.Content(DagWalkerCorpus.SharedLeaf().PruneAfter(node => node == "middle").TakeDownstreamWhere(node => node == "alpha")),
          DagWalkerCorpus.Content(alpha.Downstream()),
          name);
        Assert.AreEqual("nodes[alpha,middle,sharedLeaf] edges[alpha->middle:0.50,alpha->sharedLeaf:0.10] sources[alpha]", DagWalkerCorpus.Content(alpha.Downstream()), name);
      }
    }
  }
}
