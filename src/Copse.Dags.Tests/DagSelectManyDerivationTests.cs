using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The derivation battery: the reshapings ARE the bind's quartet, content-exact over the
  // corpus -- Select is Return, PruneBefore is Drop-or-Return, PruneAfter is Leaf-or-Return,
  // Where is Promote-or-Return with the same composer. This is the claim the algebra's
  // completeness rests on ("{Select, Where, PruneBefore, PruneAfter} all bind-derivable", the
  // tree family's 2026-08-21 result), made true dag-side. The operators keep their seats as
  // fast paths; the bind is the definition.
  [TestClass]
  public class DagSelectManyDerivationTests
  {
    private static decimal Times(decimal upstream, decimal downstream) => upstream * downstream;

    private static IEnumerable<(string Name, Func<Dag<string, decimal>> Factory)> Corpus()
      => DagWalkerCorpus.All().Concat(new[] { ("parallel", (Func<Dag<string, decimal>>)ParallelEdges) });

    // top -> mid twice (0.25, 0.75), mid -> bottom, top -> bottom: parallel edges and a skip.
    private static Dag<string, decimal> ParallelEdges()
    {
      var top = new DagNode<string, decimal>("top");
      var mid = top.AddChild("mid", 0.25m);
      top.AddChild(mid, 0.75m);
      var bottom = mid.AddChild("bottom", 0.5m);
      top.AddChild(bottom, 0.1m);
      return new Dag<string, decimal>(top);
    }

    private static IEnumerable<Func<string, bool>> Predicates()
    {
      yield return node => node == "left";
      yield return node => node == "venture" || node == "sharedLeaf" || node == "bottom";
      yield return node => node == "apex" || node == "alpha" || node == "top" || node == "a" || node == "island1";
      yield return node => node == "mid" || node == "middle" || node == "b";
      yield return node => true;
      yield return node => false;
    }

    [TestMethod]
    public void Select_IsBindOfReturn()
    {
      foreach (var (name, factory) in Corpus())
        Assert.AreEqual(
          DagWalkerCorpus.Content(factory().Select(node => node.ToUpperInvariant())),
          DagWalkerCorpus.Content(factory().SelectMany(node => DagExpansion<string, decimal>.Return(node.ToUpperInvariant()), Times)),
          name);
    }

    [TestMethod]
    public void PruneBefore_IsBindOfDropOrReturn()
    {
      foreach (var (name, factory) in Corpus())
        foreach (var predicate in Predicates())
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().PruneBefore(predicate)),
            DagWalkerCorpus.Content(factory().SelectMany(node => predicate(node) ? DagExpansion<string, decimal>.Drop : DagExpansion<string, decimal>.Return(node), Times)),
            name);
    }

    [TestMethod]
    public void PruneAfter_IsBindOfLeafOrReturn()
    {
      foreach (var (name, factory) in Corpus())
        foreach (var predicate in Predicates())
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().PruneAfter(predicate)),
            DagWalkerCorpus.Content(factory().SelectMany(node => predicate(node) ? DagExpansion<string, decimal>.Leaf(node) : DagExpansion<string, decimal>.Return(node), Times)),
            name);
    }

    [TestMethod]
    public void Where_IsBindOfReturnOrPromote_WithTheSameComposer()
    {
      // Where keeps on true; the bind keeps with Return and dissolves with Promote, the
      // composer shared -- the bypass's (inEdge ∘ outEdge) IS the bind's (upstream ∘ downstream).
      foreach (var (name, factory) in Corpus())
        foreach (var predicate in Predicates())
          Assert.AreEqual(
            DagWalkerCorpus.Content(factory().Where(predicate, Times)),
            DagWalkerCorpus.Content(factory().SelectMany(node => predicate(node) ? DagExpansion<string, decimal>.Return(node) : DagExpansion<string, decimal>.Promote, Times)),
            name);
    }

    [TestMethod]
    public void TheDiamondsLookthrough_SurvivesEveryDerivedReshaping()
    {
      // Dissolving the middles composes 60%×70% + 40%×30% into two parallel apex→venture
      // edges summing to 54% -- the lookthrough, by bypass, by bind.
      var dissolved = DagWalkerCorpus.Diamond().SelectMany(
        node => node == "left" || node == "right" ? DagExpansion<string, decimal>.Promote : DagExpansion<string, decimal>.Return(node),
        Times);

      CollectionAssert.AreEqual(new[] { "apex->venture:0.12", "apex->venture:0.42" }, DagWalkerCorpus.Edges(dissolved));
      Assert.AreEqual(0.54m, dissolved.GetEdges().Sum(edge => edge.Edge));
    }

    [TestMethod]
    public void PromotingASource_MakesItsChildrenSources()
    {
      var promoted = DagWalkerCorpus.Diamond().SelectMany(
        node => node == "apex" ? DagExpansion<string, decimal>.Promote : DagExpansion<string, decimal>.Return(node),
        Times);

      Assert.AreEqual("nodes[left,right,venture] edges[left->venture:0.70,right->venture:0.30] sources[left,right]", DagWalkerCorpus.Content(promoted));
    }

    [TestMethod]
    public void SingleNodeExpansions_KeepTheSeat_FragmentsAreBornHere()
    {
      var bound = DagWalkerCorpus.Chain().SelectMany(
        node => node == "b"
          ? DagExpansion<string, decimal>.Of(new[] { "b1", "b2" }, new[] { (0, 1, 2m) }, DagSlot<decimal>.Under(1))
          : DagExpansion<string, decimal>.Return(node),
        Times);

      CollectionAssert.AreEqual(new[] { "a", "b1", "b2", "c" }, bound.GetTopologicalOrder().ToList());
      Assert.AreEqual(0, bound.SourceOrdinal(0), "a keeps its seat");
      Assert.AreEqual(-1, bound.SourceOrdinal(1), "b1 is born here");
      Assert.AreEqual(-1, bound.SourceOrdinal(2), "b2 is born here");
      Assert.AreEqual(2, bound.SourceOrdinal(3), "c keeps its seat");
      CollectionAssert.AreEqual(new[] { "a->b1:1.00", "b1->b2:2.00", "b2->c:1.00" }, DagWalkerCorpus.Edges(bound));
    }
  }
}
