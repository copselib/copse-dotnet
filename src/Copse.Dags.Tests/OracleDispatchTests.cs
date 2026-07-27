using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  [TestClass]
  public class OracleDispatchTests
  {
    // Weighted diamond: apex --0.5--> left, --0.5--> right; left --0.6--> shared, right --0.4--> shared.
    private static (Dag<string, decimal> Dag, DagNode<string, decimal> Apex, DagNode<string, decimal> Left, DagNode<string, decimal> Right, DagNode<string, decimal> Shared) BuildWeightedDiamond()
    {
      var apex = new DagNode<string, decimal>("apex");
      var left = apex.AddChild(new DagNode<string, decimal>("left"), 0.5m);
      var right = apex.AddChild(new DagNode<string, decimal>("right"), 0.5m);
      var shared = new DagNode<string, decimal>("shared");
      left.AddChild(shared, 0.6m);
      right.AddChild(shared, 0.4m);

      return (new Dag<string, decimal>(apex), apex, left, right, shared);
    }

    [TestMethod]
    public void SourcefixDispatch_PairsEveryValueWithWhatArrived_EdgesCarried()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();

      var dispatched = dag.OracleSourcefixDispatch<string, decimal, decimal>(
        mergeInflows: (node, inflows) => inflows.Count == 0 ? 100m : inflows.Sum(),
        survey: (node, arrived, children) =>
        {
          foreach (var child in children)
            child.Dispatch(arrived / children.Count);
        });

      var dispatchedByValue = dispatched.GetTopologicalOrder()
        .ToDictionary(node => node.Value.Value, node => node.Value.Dispatched);

      Assert.AreEqual(100m, dispatchedByValue["apex"]);
      Assert.AreEqual(50m, dispatchedByValue["left"]);
      Assert.AreEqual(50m, dispatchedByValue["right"]);
      Assert.AreEqual(100m, dispatchedByValue["shared"]); // merged from both parents

      // Decoration, not replacement: shape (4 nodes, shared still shared) and payloads carried.
      Assert.AreEqual(4, dispatched.GetTopologicalOrder().Count);
      Assert.AreEqual(0.5m, dispatched.Sources[0].ChildEdges[0].Value);
      Assert.AreSame(
        dispatched.Sources[0].Children[0].Children[0],
        dispatched.Sources[0].Children[1].Children[0]);
    }

    [TestMethod]
    public void Targets_ExposeThisEdgesPayload_PerParent()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();
      var edgeSeenAtSharedByParent = new Dictionary<string, decimal>();

      dag.OracleSourcefixDispatch<string, decimal, int>(
        mergeInflows: (node, inflows) => 0,
        survey: (node, arrived, children) =>
        {
          foreach (var child in children)
          {
            if (child.Node.Value == "shared")
              edgeSeenAtSharedByParent[node.Value] = child.Edge;

            child.Dispatch(0);
          }
        });

      // The SAME shared child is a different target, with a different payload, under each parent.
      Assert.AreEqual(0.6m, edgeSeenAtSharedByParent["left"]);
      Assert.AreEqual(0.4m, edgeSeenAtSharedByParent["right"]);
    }

    [TestMethod]
    public void LeavesAreNotSurveyed()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();
      var surveyedValues = new List<string>();

      dag.OracleSourcefixDispatch<string, decimal, int>(
        mergeInflows: (node, inflows) => 0,
        survey: (node, arrived, children) =>
        {
          surveyedValues.Add(node.Value);
          foreach (var child in children)
            child.Dispatch(0);
        });

      CollectionAssert.AreEquivalent(new[] { "apex", "left", "right" }, surveyedValues);
    }

    [TestMethod]
    public void MissingDispatch_Throws()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();

      var exception = Assert.ThrowsException<InvalidOperationException>(() =>
        dag.OracleSourcefixDispatch<string, decimal, int>(
          mergeInflows: (node, inflows) => 0,
          survey: (node, arrived, children) => children[0].Dispatch(0))); // later siblings skipped

      StringAssert.Contains(exception.Message, "without dispatching");
    }

    [TestMethod]
    public void DoubleDispatch_Throws()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();

      var exception = Assert.ThrowsException<InvalidOperationException>(() =>
        dag.OracleSourcefixDispatch<string, decimal, int>(
          mergeInflows: (node, inflows) => 0,
          survey: (node, arrived, children) =>
          {
            children[0].Dispatch(0);
            children[0].Dispatch(0);
          }));

      StringAssert.Contains(exception.Message, "twice");
    }

    [TestMethod]
    public void Do_IsEager_TopologicallyOrdered_AndChains()
    {
      var (dag, _, _, _, _) = BuildWeightedDiamond();
      var visitedValues = new List<string>();

      var returned = dag.OracleDo(node => visitedValues.Add(node.Value));

      Assert.AreSame(dag, returned);
      Assert.AreEqual(4, visitedValues.Count); // ran eagerly, once, each node once
      Assert.AreEqual("apex", visitedValues[0]);
      Assert.AreEqual("shared", visitedValues[3]);
    }

    [TestMethod]
    public void WorkShapedAllocator_PlugsIn_BlockersPruned_AppliedWithDo_RolledUpWithSinkfixScan()
    {
      // The full composed methodology, dispatch-form: the setter-callback allocator drops into
      // the survey with no adapter code -- (child, amount) => child.Dispatch(amount) IS the
      // allocator's assignment callback.
      var fund = new DagNode<LegalEntity, decimal>(new LegalEntity("fund"));
      var opCoA = fund.AddChild(new DagNode<LegalEntity, decimal>(new LegalEntity("opCoA")), 0.60m);
      var blocker = fund.AddChild(new DagNode<LegalEntity, decimal>(new LegalEntity("blocker", isBlocker: true)), 0.20m);
      var opCoB = fund.AddChild(new DagNode<LegalEntity, decimal>(new LegalEntity("opCoB")), 0.20m);
      var trappedOpCo = blocker.AddChild(new DagNode<LegalEntity, decimal>(new LegalEntity("trappedOpCo")), 1.00m);

      opCoA.Value.Portfolios.AddRange(new[] { new Portfolio("pA1", 50m), new Portfolio("pA2", 50m) });
      opCoB.Value.Portfolios.Add(new Portfolio("pB", 100m));
      trappedOpCo.Value.Portfolios.Add(new Portfolio("pTrapped", 100m));

      var structure = new Dag<LegalEntity, decimal>(fund);

      var dispatched = structure
        .OraclePruneBefore(entityNode => entityNode.Value.IsBlocker)
        .OracleSourcefixDispatch<LegalEntity, decimal, decimal>(
          mergeInflows: (entityNode, inflows) => inflows.Count == 0 ? 1_000.00m : inflows.Sum(),
          survey: (entityNode, arrivedAmount, children) =>
            AmountAllocator.AllocateWithRounding(arrivedAmount,
                                                 2,
                                                 children,
                                                 child => child.Edge,
                                                 (child, childAmount) => child.Dispatch(childAmount),
                                                 AmountAllocator.InputValidation.Strict));

      // Apply: leaves of the flow graph invest their arrival across portfolios (same allocator).
      dispatched.OracleDo(node =>
      {
        if (node.Children.Count > 0)
          return;

        AmountAllocator.AllocateWithRounding(node.Value.Dispatched,
                                             2,
                                             node.Value.Value.Portfolios,
                                             portfolio => portfolio.Weight,
                                             (portfolio, amount) => portfolio.Money += amount,
                                             AmountAllocator.InputValidation.Strict);
      });

      // Roll up over the SAME dispatched dag.
      var rolledUp = dispatched.OracleSinkfixScan<DispatchNode<LegalEntity, decimal>, decimal, decimal>(
        (node, childTotals) => childTotals.Count == 0 ? node.Value.Dispatched : childTotals.Sum());

      var dispatchedByEntity = dispatched.GetTopologicalOrder()
        .ToDictionary(node => node.Value.Value, node => node.Value.Dispatched);

      Assert.AreEqual(750.00m, dispatchedByEntity[opCoA.Value]);   // 0.60 / 0.80 renormalized
      Assert.AreEqual(250.00m, dispatchedByEntity[opCoB.Value]);   // 0.20 / 0.80
      Assert.IsFalse(dispatchedByEntity.ContainsKey(blocker.Value));
      Assert.IsFalse(dispatchedByEntity.ContainsKey(trappedOpCo.Value));

      Assert.AreEqual(375.00m, opCoA.Value.Portfolios[0].Money);
      Assert.AreEqual(375.00m, opCoA.Value.Portfolios[1].Money);
      Assert.AreEqual(250.00m, opCoB.Value.Portfolios[0].Money);
      Assert.AreEqual(0m, trappedOpCo.Value.Portfolios[0].Money);

      Assert.AreEqual(1_000.00m, rolledUp.Sources[0].Value); // exact conservation
    }
  }
}
