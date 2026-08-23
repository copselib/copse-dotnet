using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // Consume: the drain-without-residency validator (the lazy builder ruling) and the
  // effect-chain terminal. Completing is the proof; the proof is about this drain.
  [TestClass]
  public class DagConsumeTests
  {
    [TestMethod]
    public void Consume_Acyclic_CompletesQuietly()
    {
      var root = new DagNode<string, int>("a");
      root.AddChild("b").AddChild("c");

      new Dag<string, int>(root).Consume();
    }

    [TestMethod]
    public void Consume_Cyclic_ThrowsAtStarvation()
    {
      var s = new DagNode<string, int>("s");
      var a = s.AddChild("a");
      a.AddChild("b").AddChild(a);

      Assert.ThrowsException<DagCycleException>(() => new Dag<string, int>(s).Consume());
    }

    [TestMethod]
    public void Consume_IsTheEffectChainTerminal()
    {
      var root = new DagNode<string, int>("a");
      root.AddChild("b");

      var visits = 0;
      new Dag<string, int>(root).Do(_ => visits++).Consume();

      // The chain a -> b: two source-block/dispatch discoveries and two entries.
      Assert.AreEqual(4, visits);
    }
  }
}
