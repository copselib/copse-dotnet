using System;
using System.Collections.Generic;
using System.Linq;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // ----- Domain stand-ins for the money-movement scenario -----
  // Ownership fractions are NOT here: they are per-edge facts (each parent owns its own slice of
  // the same child), so they ride the dag's TEdge payload and survive prune/scan/select clones.

  internal sealed class LegalEntity
  {
    public LegalEntity(string name, bool isBlocker = false)
    {
      Name = name;
      IsBlocker = isBlocker;
    }

    public string Name { get; }
    public bool IsBlocker { get; }
    public List<Portfolio> Portfolios { get; } = new List<Portfolio>();

    public override string ToString() => Name;
  }

  internal sealed class Portfolio
  {
    public Portfolio(string name, decimal weight)
    {
      Name = name;
      Weight = weight;
    }

    public string Name { get; }
    public decimal Weight { get; }
    public decimal Money { get; set; }
  }

  // Stand-in for the real fairness algorithm: largest-remainder allocation, in the WORK API's
  // shape -- setter-callback assignment, rounding decimals, validation mode. The two properties
  // the passes below rely on: EXACTNESS (outputs sum to precisely the input) and renormalization
  // (weights need not sum to 1 -- surviving siblings split pro rata after a blocker is pruned).
  internal static class AmountAllocator
  {
    public enum InputValidation { Strict }

    public static void AllocateWithRounding<TItem>(
      decimal amount,
      int decimals,
      IReadOnlyList<TItem> items,
      Func<TItem, decimal> weightSelector,
      Action<TItem, decimal> assignAmount,
      InputValidation validation)
    {
      var scale = 1m;
      for (var decimalIndex = 0; decimalIndex < decimals; decimalIndex++)
        scale *= 10m;

      var weights = items.Select(weightSelector).ToList();
      var totalWeight = weights.Sum();

      if (validation == InputValidation.Strict && totalWeight <= 0m)
        throw new ArgumentException("Weights must sum to a positive total.", nameof(items));

      var flooredAmounts = weights
        .Select(weight => Math.Floor(amount * weight / totalWeight * scale) / scale)
        .ToArray();

      var unitShortfall = (int)Math.Round((amount - flooredAmounts.Sum()) * scale);
      var indexesByRemainder = Enumerable.Range(0, items.Count)
        .OrderByDescending(itemIndex => amount * weights[itemIndex] / totalWeight - flooredAmounts[itemIndex])
        .ToList();

      for (var unitIndex = 0; unitIndex < unitShortfall; unitIndex++)
        flooredAmounts[indexesByRemainder[unitIndex % items.Count]] += 1m / scale;

      for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        assignAmount(items[itemIndex], flooredAmounts[itemIndex]);
    }

    public static IReadOnlyList<decimal> Allocate(decimal amount, IReadOnlyList<decimal> weights)
    {
      var amounts = new decimal[weights.Count];

      AllocateWithRounding(
        amount,
        2,
        Enumerable.Range(0, weights.Count).ToList(),
        weightIndex => weights[weightIndex],
        (weightIndex, allocatedAmount) => amounts[weightIndex] = allocatedAmount,
        InputValidation.Strict);

      return amounts;
    }
  }

  [TestClass]
  public class MoneyMovementScenarioTests
  {
    // ----- The whole methodology, composed. This is the payload of the spike: the traversal
    // complexity (topological ordering, inflow merging at joint ventures, reachability after
    // blockers) lives in the library; the business logic is these three lambdas. -----

    private static IReadOnlyDictionary<DagNode<LegalEntity, decimal>, decimal> MoveMoney(
      Dag<LegalEntity, decimal> flowGraph, decimal startingAmount)
    {
      // Pass 1, downward: distribute pro rata by edge ownership, fairness-allocated per edge.
      // (SourcefixAllocate, not SourcefixScan: money SPLITS across out-edges; a scan would copy.)
      var entityAmounts = flowGraph.OracleSourcefixAllocate<LegalEntity, decimal, decimal>(
        mergeInflows: (entityNode, inflows) =>
          inflows.Count == 0 ? startingAmount : inflows.Sum(),
        allocateToChildren: (entityNode, entityAmount) =>
          AmountAllocator.Allocate(entityAmount, entityNode.ChildEdges.Select(edge => edge.Value).ToList()));

      // Pass 2, upward: leaves of the FLOW GRAPH split their arrival across their portfolios
      // (same fairness algorithm), then every node sums the portfolio-level money beneath it.
      flowGraph.OracleSinkfixScan<LegalEntity, decimal, decimal>((entityNode, childTotals) =>
      {
        if (childTotals.Count > 0)
          return childTotals.Sum();

        var entity = entityNode.Value;
        var portfolioAmounts = AmountAllocator.Allocate(
          entityAmounts[entityNode],
          entity.Portfolios.Select(portfolio => portfolio.Weight).ToList());

        for (var portfolioIndex = 0; portfolioIndex < entity.Portfolios.Count; portfolioIndex++)
          entity.Portfolios[portfolioIndex].Money += portfolioAmounts[portfolioIndex];

        return portfolioAmounts.Sum();
      });

      return entityAmounts;
    }

    private static DagNode<LegalEntity, decimal> NewEntityNode(string name, bool isBlocker = false) =>
      new DagNode<LegalEntity, decimal>(new LegalEntity(name, isBlocker));

    [TestMethod]
    public void MoneyFlowsDownOwnership_SplitsIntoLeafPortfolios_AndRollsBackUp()
    {
      // fund --60%--> holdCoA --50/50--> opCo1, opCo2
      //      --40%--> holdCoB --100%---> opCo3
      var fund = NewEntityNode("fund");
      var holdCoA = fund.AddChild(NewEntityNode("holdCoA"), 0.60m);
      var holdCoB = fund.AddChild(NewEntityNode("holdCoB"), 0.40m);
      var opCo1 = holdCoA.AddChild(NewEntityNode("opCo1"), 0.50m);
      var opCo2 = holdCoA.AddChild(NewEntityNode("opCo2"), 0.50m);
      var opCo3 = holdCoB.AddChild(NewEntityNode("opCo3"), 1.00m);

      opCo1.Value.Portfolios.AddRange(new[] { new Portfolio("p1a", 50m), new Portfolio("p1b", 30m), new Portfolio("p1c", 20m) });
      opCo2.Value.Portfolios.AddRange(new[] { new Portfolio("p2a", 100m / 3m), new Portfolio("p2b", 100m / 3m), new Portfolio("p2c", 100m / 3m) });
      opCo3.Value.Portfolios.Add(new Portfolio("p3a", 100m));

      var structure = new Dag<LegalEntity, decimal>(fund);

      var entityAmounts = MoveMoney(structure, 1_000_000.00m);

      Assert.AreEqual(1_000_000.00m, entityAmounts[fund]);
      Assert.AreEqual(600_000.00m, entityAmounts[holdCoA]);
      Assert.AreEqual(400_000.00m, entityAmounts[holdCoB]);
      Assert.AreEqual(300_000.00m, entityAmounts[opCo1]);
      Assert.AreEqual(300_000.00m, entityAmounts[opCo2]);
      Assert.AreEqual(400_000.00m, entityAmounts[opCo3]);

      Assert.AreEqual(150_000.00m, opCo1.Value.Portfolios[0].Money);
      Assert.AreEqual(90_000.00m, opCo1.Value.Portfolios[1].Money);
      Assert.AreEqual(60_000.00m, opCo1.Value.Portfolios[2].Money);

      // Conservation, exactly: every penny that went in is sitting in a portfolio.
      var allPortfolios = structure.GetTopologicalOrder().SelectMany(node => node.Value.Portfolios);
      Assert.AreEqual(1_000_000.00m, allPortfolios.Sum(portfolio => portfolio.Money));
    }

    [TestMethod]
    public void BlockerEntities_AreExcludedFromTheFlow_AndMoneyReroutesProRata()
    {
      // fund --60%--> opCoA, --20%--> blocker, --20%--> opCoB;  blocker --100%--> trappedOpCo.
      var fund = NewEntityNode("fund");
      var opCoA = fund.AddChild(NewEntityNode("opCoA"), 0.60m);
      var blocker = fund.AddChild(NewEntityNode("blocker", isBlocker: true), 0.20m);
      var opCoB = fund.AddChild(NewEntityNode("opCoB"), 0.20m);
      var trappedOpCo = blocker.AddChild(NewEntityNode("trappedOpCo"), 1.00m);

      opCoA.Value.Portfolios.Add(new Portfolio("pA", 100m));
      opCoB.Value.Portfolios.Add(new Portfolio("pB", 100m));
      trappedOpCo.Value.Portfolios.Add(new Portfolio("pTrapped", 100m));

      var structure = new Dag<LegalEntity, decimal>(fund);

      // The composition the whole spike is about: the blocker rule is ONE prune in front of the
      // same MoveMoney the no-blocker scenario uses. Surviving edges keep their ownership
      // fractions, and the fairness allocator renormalizes 60/20 across the survivors.
      var flowGraph = structure.OraclePruneBefore(entityNode => entityNode.Value.IsBlocker);
      var entityAmounts = MoveMoney(flowGraph, 1_000.00m);

      // The flow graph's nodes are fresh wrappers; correlate through the shared entity values.
      var amountsByEntity = entityAmounts.ToDictionary(pair => pair.Key.Value, pair => pair.Value);
      Assert.AreEqual(750.00m, amountsByEntity[opCoA.Value]);   // 0.60 / 0.80
      Assert.AreEqual(250.00m, amountsByEntity[opCoB.Value]);   // 0.20 / 0.80
      Assert.IsFalse(amountsByEntity.ContainsKey(blocker.Value));
      Assert.IsFalse(amountsByEntity.ContainsKey(trappedOpCo.Value));

      Assert.AreEqual(750.00m, opCoA.Value.Portfolios[0].Money);
      Assert.AreEqual(250.00m, opCoB.Value.Portfolios[0].Money);
      Assert.AreEqual(0m, trappedOpCo.Value.Portfolios[0].Money); // nothing moved through the blocker

      // And the source structure still has the blocker: the prune composed, it didn't mutate.
      Assert.AreEqual(3, fund.Children.Count);
    }

    [TestMethod]
    public void BlockedJointVenture_StillReceives_ViaItsSurvivingOwner()
    {
      // jvOpCo is owned by a blocker (60%) AND a clean holdco (40%): pruning the blocker must
      // sever only the blocked path -- the venture stays reachable and receives via holdCo alone.
      var fund = NewEntityNode("fund");
      var holdCo = fund.AddChild(NewEntityNode("holdCo"), 0.50m);
      var blocker = fund.AddChild(NewEntityNode("blocker", isBlocker: true), 0.50m);
      var jvOpCo = NewEntityNode("jvOpCo");
      holdCo.AddChild(jvOpCo, 0.40m);
      blocker.AddChild(jvOpCo, 0.60m);
      jvOpCo.Value.Portfolios.Add(new Portfolio("pJv", 100m));

      var structure = new Dag<LegalEntity, decimal>(fund);

      var flowGraph = structure.OraclePruneBefore(entityNode => entityNode.Value.IsBlocker);
      var entityAmounts = MoveMoney(flowGraph, 1_000.00m);

      var amountsByEntity = entityAmounts.ToDictionary(pair => pair.Key.Value, pair => pair.Value);

      // fund's survivors renormalize to holdCo alone; holdCo's only surviving edge takes all.
      Assert.AreEqual(1_000.00m, amountsByEntity[holdCo.Value]);
      Assert.AreEqual(1_000.00m, amountsByEntity[jvOpCo.Value]);
      Assert.AreEqual(1_000.00m, jvOpCo.Value.Portfolios[0].Money);

      // The venture's surviving in-edge kept its payload through the prune.
      var jvFlowNode = flowGraph.GetTopologicalOrder().Single(node => node.Value == jvOpCo.Value);
      Assert.AreEqual(1, jvFlowNode.ParentEdges.Count);
      Assert.AreEqual(0.40m, jvFlowNode.ParentEdges[0].Value);
    }

    [TestMethod]
    public void HoldingBlockers_ReceiveButDoNotPassOn_WithPruneAfter()
    {
      // The other plausible blocker rule: money REACHES the blocker (it keeps its pro rata
      // share) but cannot move through it. That is PruneAfter -- the blocker becomes a sink.
      var fund = NewEntityNode("fund");
      var opCo = fund.AddChild(NewEntityNode("opCo"), 0.60m);
      var blocker = fund.AddChild(NewEntityNode("blocker", isBlocker: true), 0.40m);
      var trappedOpCo = blocker.AddChild(NewEntityNode("trappedOpCo"), 1.00m);

      opCo.Value.Portfolios.Add(new Portfolio("pOp", 100m));
      blocker.Value.Portfolios.Add(new Portfolio("pBlockerCash", 100m));

      var structure = new Dag<LegalEntity, decimal>(fund);

      var flowGraph = structure.OraclePruneAfter(entityNode => entityNode.Value.IsBlocker);
      var entityAmounts = MoveMoney(flowGraph, 1_000.00m);

      var amountsByEntity = entityAmounts.ToDictionary(pair => pair.Key.Value, pair => pair.Value);
      Assert.AreEqual(600.00m, amountsByEntity[opCo.Value]);
      Assert.AreEqual(400.00m, amountsByEntity[blocker.Value]);            // received...
      Assert.IsFalse(amountsByEntity.ContainsKey(trappedOpCo.Value));      // ...but passed nothing on
      Assert.AreEqual(400.00m, blocker.Value.Portfolios[0].Money);       // parked as a flow-graph leaf
    }

    [TestMethod]
    public void AmountAllocator_StandIn_IsExactAtThePennyLevel()
    {
      var amounts = AmountAllocator.Allocate(100.00m, new[] { 1m, 1m, 1m });

      Assert.AreEqual(100.00m, amounts.Sum());
      CollectionAssert.AreEquivalent(new[] { 33.34m, 33.33m, 33.33m }, amounts.ToList());
    }
  }
}
