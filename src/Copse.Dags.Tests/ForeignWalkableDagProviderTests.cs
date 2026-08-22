using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The provider-mint pin (the tree family's ForeignWalkableProviderTests, dualized): a walkable
  // dag implemented ENTIRELY OUTSIDE the family (FamilyFreeDag -- two dictionaries, string
  // handles, no ordinals), minting walkers through the PUBLIC DagWalker constructor and streaming
  // through the public Dag.FromTopology. Copse.Dags grants no InternalsVisibleTo to anyone, so
  // this file COMPILING is itself the proof that the contract is implementable by third parties.
  [TestClass]
  public class ForeignWalkableDagProviderTests
  {
    [TestMethod]
    public void TheDoorMintsOverNativeAdjacency()
    {
      var door = new FamilyFreeDag().GetDagWalker();
      Assert.IsFalse(door.HasFocus, "the door stands at the unfocused stance, above the sources");
      Assert.AreEqual("apex", door.MoveToChild(0).Value.Focus, "the source is the unfocused stance's first child");
      Assert.AreEqual("apex", door.MoveToChild(0).Value.GetValue());
    }

    [TestMethod]
    public void StepsWalkTheProviderTopology_EdgeAtomic()
    {
      var apex = new FamilyFreeDag().GetDagWalker().MoveToChild(0).Value;
      var left = apex.MoveToChild(0);
      Assert.AreEqual("left", left.Value.Focus);
      Assert.AreEqual(0.60m, left.Edge, "the step carries the payload crossed");
      var venture = left.Value.MoveToChild(0);
      Assert.AreEqual("venture", venture.Value.Focus);
      Assert.AreEqual(0.70m, venture.Edge);
      Assert.AreEqual("right", venture.Value.MoveToParent(1).Value.Focus, "up the OTHER in-edge");
      Assert.AreEqual(0.30m, venture.Value.MoveToParent(1).Edge);
      Assert.IsFalse(apex.MoveToChild(2).HasValue);

      var top = apex.MoveToParent(0);
      Assert.IsTrue(top.HasValue, "a source's parent is the unfocused stance");
      Assert.IsFalse(top.Value.HasFocus);
      Assert.IsFalse(top.Value.MoveToParent(0).HasValue, "stepping up from the unfocused stance is the one upward miss");
    }

    [TestMethod]
    public void TheJumpReEntersOnStoredProviderHandles()
    {
      var walker = new FamilyFreeDag().GetDagWalker();
      Assert.AreEqual("venture", walker.At("venture").GetValue());
      Assert.AreEqual("left", walker.At("venture").MoveToParent(0).Value.Focus);
    }

    [TestMethod]
    public void BothSurfacesCoexistOnOneProvider()
    {
      var dag = new FamilyFreeDag();
      CollectionAssert.AreEqual(new[] { "apex", "left", "right", "venture" }, dag.GetTopologicalOrder().ToArray());
      CollectionAssert.AreEqual(new[] { "apex->left:0.60", "apex->right:0.40", "left->venture:0.70", "right->venture:0.30" }, DagWalkerCorpus.Edges(dag));
      Assert.AreEqual("venture", dag.GetDagWalker().At("left").MoveToChild(0).Value.Focus);
      Assert.AreEqual(0.54m, dag.SourcefixScan<string, decimal, decimal>((node, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge)).GetTopologicalOrder().Single(result => result.Node == "venture").Accumulate, "the streaming algebra runs over the foreign provider: lookthrough 54%");
    }
  }
}
