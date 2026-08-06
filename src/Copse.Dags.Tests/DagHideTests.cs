using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // Hide's seat in the three-tier stability story (the lazy builder ruling): the builder
  // guarantees nothing, Hide guarantees the CONSUMER can't mutate (no cast back), the buffer
  // guarantees nobody can. Identity laundering, not stabilization -- the owner mutating
  // behind a Hide is lawful, and drains differ.
  [TestClass]
  public class DagHideTests
  {
    private static Dag<string, int> Chain()
    {
      var root = new DagNode<string, int>("a");
      root.AddChild("b").AddChild("c");
      return new Dag<string, int>(root);
    }

    [TestMethod]
    public void Hide_LaundersTheConcreteType()
    {
      var dag = Chain();

      Assert.IsInstanceOfType(dag.Hide(), typeof(IDagnumerable<string, int>));
      Assert.IsNotInstanceOfType(dag.Hide(), typeof(Dag<string, int>));
      Assert.IsNotInstanceOfType(dag.Materialize().Hide(), typeof(DagBuffer<string, int>));
    }

    [TestMethod]
    public void Hide_ForwardsTheStreamUnchanged()
    {
      CollectionAssert.AreEqual(
        ((IDagnumerable<string, int>)Chain()).GetTopologicalOrder().ToArray(),
        ((IDagnumerable<string, int>)Chain()).Hide().GetTopologicalOrder().ToArray());

      CollectionAssert.AreEqual(
        Chain().GetEdges().Select(e => $"{e.Parent}->{e.Child}").ToArray(),
        ((IDagnumerable<string, int>)Chain()).Hide().GetEdges().Select(e => $"{e.Parent}->{e.Child}").ToArray());
    }

    [TestMethod]
    public void Hide_IsNotAStabilityPromise_TheOwnerStillMutatesBehindIt()
    {
      var root = new DagNode<string, int>("a");
      root.AddChild("b");
      var dag = new Dag<string, int>(root);
      var hidden = dag.Hide();

      CollectionAssert.AreEqual(new[] { "a", "b" }, hidden.GetTopologicalOrder().ToArray());

      root.AddChild("c");

      CollectionAssert.AreEqual(new[] { "a", "b", "c" }, hidden.GetTopologicalOrder().ToArray());
    }

    [TestMethod]
    public void Hide_ComposesThroughTheFluentSurface()
    {
      var upper = Chain().Hide().Select(n => n.ToUpperInvariant()).PruneBefore(n => n == "C");

      CollectionAssert.AreEqual(new[] { "A", "B" }, upper.GetTopologicalOrder().ToArray());
    }
  }
}
