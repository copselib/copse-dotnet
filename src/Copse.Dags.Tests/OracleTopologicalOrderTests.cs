using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The oracle's own sort, pinned in isolation: the conformance battery trusts
  // OracleTopologicalOrder as the independent statement of topological order (parents before
  // children, each node once, discovery-biased; cycles named), so the oracle must be right on
  // its own terms before it can judge the walk. Node-identity assertions, since the oracle
  // answers in owned nodes.
  [TestClass]
  public class OracleTopologicalOrderTests
  {
    [TestMethod]
    public void TopologicalOrder_Diamond_ParentsBeforeChildren_SharedNodeOnce()
    {
      var apex = new DagNode<string, int>("apex");
      var left = apex.AddChild("left");
      var right = apex.AddChild("right");
      var shared = new DagNode<string, int>("shared");
      left.AddChild(shared);
      right.AddChild(shared);

      var topologicalOrder = new Dag<string, int>(apex).OracleTopologicalOrder();

      Assert.AreEqual(4, topologicalOrder.Count);
      Assert.AreSame(apex, topologicalOrder[0]);
      Assert.AreSame(shared, topologicalOrder[3]);
      CollectionAssert.Contains(topologicalOrder.ToList(), left);
      CollectionAssert.Contains(topologicalOrder.ToList(), right);
    }

    [TestMethod]
    public void TopologicalOrder_EveryParentPrecedesItsChildren()
    {
      // A less regular shape: two roots, cross-links, a long-way-around edge.
      var rootA = new DagNode<string, int>("rootA");
      var rootB = new DagNode<string, int>("rootB");
      var middle = rootA.AddChild("middle");
      rootB.AddChild(middle);
      var deep = middle.AddChild("deep");
      rootA.AddChild(deep); // long-way-around: also a direct edge

      var topologicalOrder = new Dag<string, int>(rootA, rootB).OracleTopologicalOrder();
      var indexByNode = topologicalOrder
        .Select((node, index) => (node, index))
        .ToDictionary(pair => pair.node, pair => pair.index);

      foreach (var node in topologicalOrder)
        foreach (var child in node.Children)
          Assert.IsTrue(indexByNode[node] < indexByNode[child],
            $"parent '{node}' must precede child '{child}'");
    }

    [TestMethod]
    public void TopologicalOrder_RootReachableFromAnotherRoot_AppearsOnce()
    {
      var first = new DagNode<string, int>("first");
      var second = new DagNode<string, int>("second");
      first.AddChild(second);

      var topologicalOrder = new Dag<string, int>(first, second).OracleTopologicalOrder();

      Assert.AreEqual(2, topologicalOrder.Count);
      Assert.AreSame(first, topologicalOrder[0]);
      Assert.AreSame(second, topologicalOrder[1]);
    }

    [TestMethod]
    public void TopologicalOrder_SameRootPassedTwice_AppearsOnce()
    {
      var root = new DagNode<string, int>("root");

      Assert.AreEqual(1, new Dag<string, int>(root, root).OracleTopologicalOrder().Count);
    }

    [TestMethod]
    public void Cycle_Throws_AndNamesTheCycle()
    {
      var top = new DagNode<string, int>("top");
      var middle = top.AddChild("middle");
      var bottom = middle.AddChild("bottom");
      bottom.AddChild(middle);

      var exception = Assert.ThrowsException<DagCycleException>(
        () => new Dag<string, int>(top).OracleTopologicalOrder());

      StringAssert.Contains(exception.Message, "middle -> bottom -> middle");
    }

    [TestMethod]
    public void SelfEdge_Throws()
    {
      var node = new DagNode<string, int>("node");
      node.AddChild(node);

      Assert.ThrowsException<DagCycleException>(() => new Dag<string, int>(node).OracleTopologicalOrder());
    }

    [TestMethod]
    public void DeepChain_DoesNotOverflowTheCallStack()
    {
      const int chainLength = 200_000;

      var root = new DagNode<int, int>(0);
      var current = root;
      for (var depth = 1; depth < chainLength; depth++)
        current = current.AddChild(depth);

      var topologicalOrder = new Dag<int, int>(root).OracleTopologicalOrder();

      Assert.AreEqual(chainLength, topologicalOrder.Count);
      Assert.AreSame(root, topologicalOrder[0]);
      Assert.AreEqual(chainLength - 1, topologicalOrder[chainLength - 1].Value);
    }

    [TestMethod]
    public void MutationBetweenOperations_IsSeenByTheNextWalk()
    {
      var root = new DagNode<string, int>("root");
      var dag = new Dag<string, int>(root);

      Assert.AreEqual(1, dag.OracleTopologicalOrder().Count);

      root.AddChild("lateArrival");

      Assert.AreEqual(2, dag.OracleTopologicalOrder().Count);
    }
  }
}
