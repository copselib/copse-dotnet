using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  [TestClass]
  public class OracleDagAggregationTests
  {
    // The canonical diamond:  apex -> left, right;  left, right -> shared.
    private static (Dag<string, int> Dag, DagNode<string, int> Apex, DagNode<string, int> Left, DagNode<string, int> Right, DagNode<string, int> Shared) BuildDiamond()
    {
      var apex = new DagNode<string, int>("apex");
      var left = apex.AddChild("left");
      var right = apex.AddChild("right");
      var shared = new DagNode<string, int>("shared");
      left.AddChild(shared);
      right.AddChild(shared);

      return (new Dag<string, int>(apex), apex, left, right, shared);
    }

    [TestMethod]
    public void SinkfixAggregate_ComputesEachNodeExactlyOnce()
    {
      var (dag, _, _, _, _) = BuildDiamond();
      var aggregateCallCount = 0;

      dag.OracleSinkfixAggregate<string, int, int>((node, childResults) =>
      {
        aggregateCallCount++;
        return 0;
      });

      Assert.AreEqual(4, aggregateCallCount);
    }

    [TestMethod]
    public void SinkfixAggregate_PerUseSemantics_SharedResultAppearsUnderEachParent()
    {
      var (dag, apex, _, _, _) = BuildDiamond();

      // Subtree node count with tree-unfolding (per-use) semantics: combining the per-edge child
      // results counts the shared node once per path, so the diamond unfolds to 5.
      var subtreeNodeCounts = dag.OracleSinkfixAggregate<string, int, int>(
        (node, childResults) => 1 + childResults.Sum());

      Assert.AreEqual(5, subtreeNodeCounts[apex]);
    }

    [TestMethod]
    public void SinkfixAggregate_DistinctSemantics_ComesFromTheTopologicalOrder()
    {
      var (dag, _, _, _, _) = BuildDiamond();

      // Shared-counted-once semantics is a fold over the distinct-node enumeration instead.
      Assert.AreEqual(4, dag.GetTopologicalOrder().Count);
    }

    [TestMethod]
    public void SinkfixAggregate_LeavesReceiveEmptyChildResults()
    {
      var (dag, _, _, _, shared) = BuildDiamond();
      var childResultCountsByNode = dag.OracleSinkfixAggregate<string, int, int>(
        (node, childResults) => childResults.Count);

      Assert.AreEqual(0, childResultCountsByNode[shared]);
    }

    [TestMethod]
    public void SinkfixAggregate_ParallelEdges_ContributeTheChildResultTwice()
    {
      var parent = new DagNode<int, int>(10);
      var child = new DagNode<int, int>(7);
      parent.AddChild(child);
      parent.AddChild(child);

      var sums = new Dag<int, int>(parent).OracleSinkfixAggregate<int, int, int>(
        (node, childResults) => node.Value + childResults.Sum());

      Assert.AreEqual(10 + 7 + 7, sums[parent]);
    }

    [TestMethod]
    public void SourcefixAllocate_Diamond_SharedChildMergesInflowsFromAllParents()
    {
      var (dag, apex, left, right, shared) = BuildDiamond();

      // Seed 100 at the root, split evenly across out-edges at every node.
      var allocations = dag.OracleSourcefixAllocate<string, int, double>(
        mergeInflows: (node, inflows) => node.Parents.Count == 0 ? 100.0 : inflows.Sum(),
        allocateToChildren: (node, allocation) =>
          node.Children.Select(child => allocation / node.Children.Count).ToList());

      Assert.AreEqual(100.0, allocations[apex]);
      Assert.AreEqual(50.0, allocations[left]);
      Assert.AreEqual(50.0, allocations[right]);
      Assert.AreEqual(100.0, allocations[shared]); // both 50s arrive before shared is processed
    }

    [TestMethod]
    public void SourcefixAllocate_InflowOrderIsParentsInTopologicalOrder()
    {
      var (dag, _, left, right, shared) = BuildDiamond();
      var inflowSourcesAtShared = new List<string>();

      dag.OracleSourcefixAllocate<string, int, string>(
        mergeInflows: (node, inflows) =>
        {
          if (ReferenceEquals(node, shared))
            inflowSourcesAtShared.AddRange(inflows);
          return node.Value;
        },
        allocateToChildren: (node, allocation) =>
          node.Children.Select(child => node.Value).ToList());

      // apex's child list is [left, right], so left precedes right topologically.
      CollectionAssert.AreEqual(new[] { "left", "right" }, inflowSourcesAtShared);
    }

    [TestMethod]
    public void SourcefixAllocate_SourcesSeedWithEmptyInflows()
    {
      var root = new DagNode<string, int>("root");
      var sawEmptyInflowsAtRoot = false;

      new Dag<string, int>(root).OracleSourcefixAllocate<string, int, int>(
        mergeInflows: (node, inflows) =>
        {
          sawEmptyInflowsAtRoot = inflows.Count == 0;
          return 0;
        },
        allocateToChildren: (node, allocation) => Array.Empty<int>());

      Assert.IsTrue(sawEmptyInflowsAtRoot);
    }

    [TestMethod]
    public void SourcefixAllocate_WrongOutflowCount_Throws()
    {
      var parent = new DagNode<string, int>("parent");
      parent.AddChild("child");

      var exception = Assert.ThrowsException<InvalidOperationException>(() =>
        new Dag<string, int>(parent).OracleSourcefixAllocate<string, int, int>(
          mergeInflows: (node, inflows) => 0,
          allocateToChildren: (node, allocation) => Array.Empty<int>()));

      StringAssert.Contains(exception.Message, "one outflow per out-edge");
    }

    [TestMethod]
    public void SourcefixAllocate_ParallelEdges_DeliverOneInflowPerEdge()
    {
      var parent = new DagNode<int, int>(0);
      var child = new DagNode<int, int>(0);
      parent.AddChild(child);
      parent.AddChild(child);

      var allocations = new Dag<int, int>(parent).OracleSourcefixAllocate<int, int, int>(
        mergeInflows: (node, inflows) => node.Parents.Count == 0 ? 10 : inflows.Sum(),
        allocateToChildren: (node, allocation) =>
          node.Children.Select(edgeTarget => allocation / node.Children.Count).ToList());

      Assert.AreEqual(10, allocations[child]); // 5 down each parallel edge, merged back to 10
    }

    [TestMethod]
    public void SortChildrenBy_DagLevel_SortsEveryReachableNodesChildList()
    {
      var root = new DagNode<int, int>(0);
      var bigChild = root.AddChild(9);
      var smallChild = root.AddChild(3);
      bigChild.AddChild(8);
      bigChild.AddChild(2);

      new Dag<int, int>(root).SortChildrenBy(node => node.Value);

      Assert.AreSame(smallChild, root.Children[0]);
      Assert.AreSame(bigChild, root.Children[1]);
      Assert.AreEqual(2, bigChild.Children[0].Value);
      Assert.AreEqual(8, bigChild.Children[1].Value);
    }
  }
}
