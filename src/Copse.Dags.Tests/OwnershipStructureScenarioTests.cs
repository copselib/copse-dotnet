using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The library's origin problem, written against the contract operators (design-docs/
  // DAG_CONTRACT_DESIGN.md, phase 6's seed): an anonymized legal-entity ownership structure --
  // two funds co-investing through a shared JV (the DAG-ness), a blocker entity, whole-cent
  // amounts with a largest-remainder penny-exact allocator -- exercising the real flows:
  // effective-ownership lookthrough, money movement down (blockers pruned, or receiving and
  // holding), NAV attribution up through the diamond, and CONSERVATION asserted end-to-end in
  // both directions. Every business rule is a composed lambda; every traversal is the library's.
  //
  //   FundA (contributes 1,000,033c)        FundB (contributes 250,000c)
  //     |-- 100% --> HoldCo                    |
  //     |             |-- 60% --> JV <-- 40% --+
  //     |             |           `-- 100% --> OpCo    (holds 80,000,000c)
  //     |             `-- 100% --> SideCo              (holds 20,000,000c)
  //     `-- 100% --> Blocker
  //                   `-- 100% --> BlockedCo           (holds  5,000,000c)
  [TestClass]
  public class OwnershipStructureScenarioTests
  {
    private sealed record Entity(
      string Name, bool IsBlocker = false, decimal ContributionCents = 0m, decimal HoldingCents = 0m);

    private static Dag<Entity, decimal> Structure()
    {
      var fundA = new DagNode<Entity, decimal>(new Entity("FundA", ContributionCents: 1_000_033m));
      var fundB = new DagNode<Entity, decimal>(new Entity("FundB", ContributionCents: 250_000m));

      var holdCo = fundA.AddChild(new Entity("HoldCo"), 1.00m);
      var blocker = fundA.AddChild(new Entity("Blocker", IsBlocker: true), 1.00m);

      var jv = holdCo.AddChild(new Entity("JV"), 0.60m);
      fundB.AddChild(jv, 0.40m);

      holdCo.AddChild(new Entity("SideCo", HoldingCents: 20_000_000m), 1.00m);
      jv.AddChild(new Entity("OpCo", HoldingCents: 80_000_000m), 1.00m);
      blocker.AddChild(new Entity("BlockedCo", HoldingCents: 5_000_000m), 1.00m);

      return new Dag<Entity, decimal>(fundA, fundB);
    }

    // The work-shaped allocator: whole cents, pro rata by edge weight, largest-remainder
    // rounding (stable: ties break by target order), and conservation is an invariant, not a
    // hope -- the dispatched cents always sum to the arrived cents exactly.
    private static void AllocateWholeCents(
      decimal arrivedCents,
      IReadOnlyList<DagDispatchTarget<Entity, decimal, decimal>> targets)
    {
      var totalWeight = targets.Sum(target => target.Edge);
      var floors = new decimal[targets.Count];
      var remainders = new decimal[targets.Count];

      for (var index = 0; index < targets.Count; index++)
      {
        var exact = arrivedCents * targets[index].Edge / totalWeight;
        floors[index] = decimal.Floor(exact);
        remainders[index] = exact - floors[index];
      }

      var leftoverCents = (int)(arrivedCents - floors.Sum());
      foreach (var index in Enumerable.Range(0, targets.Count)
        .OrderByDescending(i => remainders[i])
        .Take(leftoverCents))
      {
        floors[index] += 1m;
      }

      for (var index = 0; index < targets.Count; index++)
        targets[index].Dispatch(floors[index]);
    }

    private static void MoveMoneySurvey(
      Entity subject,
      IReadOnlyList<DagDispatchInflow<Entity, decimal, decimal>> arrivals,
      IReadOnlyList<DagDispatchTarget<Entity, decimal, decimal>> targets)
    {
      var arrived = arrivals.Sum(arrival => arrival.Value);

      // The virtual source family, surveyed first (full participation): its targets
      // are the funds and carry no ownership weight, so there is nothing to allocate pro rata --
      // the seed reaches each source verbatim, exactly the pre-re-founding semantics. (Each
      // fund's OWN contribution is added at its own survey, below.)
      if (subject is null)
      {
        foreach (var target in targets)
          target.Dispatch(arrived);
        return;
      }

      AllocateWholeCents(subject.ContributionCents + arrived, targets);
    }

    private static void AttributeUpSurvey(
      Entity subject,
      IReadOnlyList<DagDispatchInflow<Entity, decimal, decimal>> arrivals,
      IReadOnlyList<DagDispatchTarget<Entity, decimal, decimal>> targets)
    {
      // No virtual family upward: holdings originate IN the nodes, so sinks simply see none.
      var total = subject.HoldingCents + arrivals.Sum(upflow => upflow.Value);
      foreach (var target in targets)
        target.Dispatch(total * target.Edge);
    }

    private static Dictionary<string, DagDispatchResult<Entity, decimal>> ByName(
      DagBuffer<DagDispatchResult<Entity, decimal>, decimal> dispatched)
      => dispatched.Values.ToDictionary(result => result.Node.Name, result => result);

    private static decimal Received(DagDispatchResult<Entity, decimal> result)
      => result.Node.ContributionCents + result.Arrivals.ToArray().Sum();

    // The post-pass arrival read with edge context: provenance no longer travels ON the result
    // (the split-homes ruling), so "what arrived on which edge" is the GetEdges join
    // keyed by in-edge index -- never a payload comparison.
    private static (decimal Amount, decimal Edge)[] ArrivalsAt(
      DagBuffer<DagDispatchResult<Entity, decimal>, decimal> dispatched, string entity)
      => dispatched.GetEdges()
        .Where(edge => edge.Child.Node.Name == entity)
        .Select(edge => (edge.Child.Arrivals[edge.InEdgeIndex], edge.Edge))
        .ToArray();

    // The upward twin: arrivals sit in OUT-edge order and GetEdges yields a parent's out-edges
    // contiguously in that order, so position within the parent's block IS the arrival index.
    private static (decimal Amount, decimal Edge)[] UpflowsAt(
      DagBuffer<DagDispatchResult<Entity, decimal>, decimal> attributed, string entity)
      => attributed.GetEdges()
        .Where(edge => edge.Parent.Node.Name == entity)
        .Select((edge, outEdgeIndex) => (edge.Parent.Arrivals[outEdgeIndex], edge.Edge))
        .ToArray();

    // ---------------------------------------------------------------------------------------
    // Effective ownership (the lookthrough report).
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void EffectiveOwnership_IsFullyAccountedEverywhere()
    {
      // Every entity's in-edge fractions sum to 100%, so combined institutional lookthrough
      // is 1.0 at every entity -- ownership neither leaks nor multiplies through the JV.
      var ownership = Structure().Sourcefix().Scan<decimal>(
        (entity, inflows) => inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value * inflow.Edge));

      foreach (var pairing in ownership.Values)
        Assert.AreEqual(1m, pairing.Accumulate);
    }

    [TestMethod]
    public void EffectiveOwnership_PerFund_ByPruningTheOtherRoot()
    {
      // Each fund's own lookthrough: prune the other root; only its component dies, and the
      // shared JV survives on the remaining route. The scan CARRIES the entity name in its
      // accumulation -- the report is one composed lambda.
      static Dictionary<string, decimal> Lookthrough(string prunedFund) => Structure()
        .PruneNodesBefore(entity => entity.Name == prunedFund)
        .Sourcefix().Scan<(string Name, decimal Ownership)>(
          (entity, inflows) => (
            entity.Name,
            inflows.Count == 0 ? 1m : inflows.Sum(inflow => inflow.Value.Ownership * inflow.Edge)))
        .Values
        .ToDictionary(pairing => pairing.Accumulate.Name, pairing => pairing.Accumulate.Ownership);

      var fundAView = Lookthrough(prunedFund: "FundB");
      Assert.AreEqual(0.60m, fundAView["JV"], "FundA reaches the JV through HoldCo only");
      Assert.AreEqual(0.60m, fundAView["OpCo"]);
      Assert.AreEqual(1m, fundAView["SideCo"]);

      var fundBView = Lookthrough(prunedFund: "FundA");
      CollectionAssert.AreEquivalent(new[] { "FundB", "JV", "OpCo" }, fundBView.Keys,
        "FundA's exclusive component is gone; the shared JV survives");
      Assert.AreEqual(0.40m, fundBView["JV"]);
      Assert.AreEqual(0.40m, fundBView["OpCo"]);
    }

    // ---------------------------------------------------------------------------------------
    // Money moves down.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void MoveMoney_BlockersPruned_PennyExact_ConservedThroughTheSharedJV()
    {
      // The MoveMoney shape: blockers pruned first, so nothing is ever allocated toward them
      // -- the allocator renormalizes over LIVE edges by construction, no special case.
      var moved = Structure()
        .PruneNodesBefore(entity => entity.IsBlocker)
        .Sourcefix().Dispatch(0m, MoveMoneySurvey);
      var byName = ByName(moved);

      // FundA's whole contribution rides the one live edge; HoldCo splits 60:100 weights
      // (375,012.375 / 625,020.625 exact) -- the odd cent goes to the larger remainder.
      Assert.AreEqual(1_000_033m, Received(byName["HoldCo"]));
      Assert.AreEqual(625_021m, Received(byName["SideCo"]));

      // The JV receives through BOTH routes -- attribution intact -- and forwards whole.
      CollectionAssert.AreEquivalent(
        new[] { (375_012m, 0.60m), (250_000m, 0.40m) },
        ArrivalsAt(moved, "JV").ToList());
      Assert.AreEqual(625_012m, Received(byName["OpCo"]));

      // Conservation, end to end: every contributed cent lands somewhere real.
      Assert.AreEqual(
        1_000_033m + 250_000m,
        Received(byName["OpCo"]) + Received(byName["SideCo"]),
        "contributions == what the operating companies received, to the cent");
      Assert.IsFalse(byName.ContainsKey("Blocker"), "pruned");
      Assert.IsFalse(byName.ContainsKey("BlockedCo"), "unreachable without the blocker");
    }

    [TestMethod]
    public void MoveMoney_BlockerReceivesButHolds_TheTrapIsVisible()
    {
      // The other blocker policy: PruneAfter -- the blocker takes its allocation and passes
      // nothing through. Money is trapped there, visibly, and still conserved.
      var moved = ByName(Structure()
        .PruneNodesAfter(entity => entity.IsBlocker)
        .Sourcefix().Dispatch(0m, MoveMoneySurvey));

      // FundA now splits 50:50 (equal weights); the odd cent breaks the tie toward HoldCo
      // (first target -- stable ordering, pinned deliberately).
      Assert.AreEqual(500_017m, Received(moved["HoldCo"]));
      Assert.AreEqual(500_016m, Received(moved["Blocker"]), "received -- and held");

      Assert.AreEqual(437_506m, Received(moved["OpCo"]), "187,506 via HoldCo + 250,000 via FundB");
      Assert.AreEqual(312_511m, Received(moved["SideCo"]));
      Assert.IsFalse(moved.ContainsKey("BlockedCo"), "nothing passes a holding blocker");

      Assert.AreEqual(
        1_000_033m + 250_000m,
        Received(moved["OpCo"]) + Received(moved["SideCo"]) + Received(moved["Blocker"]),
        "conserved: reached the real economy or trapped at the blocker, to the cent");
    }

    [TestMethod]
    public void MoveMoney_EveryIntermediateForwardsExactlyWhatItReceived()
    {
      var moved = Structure()
        .PruneNodesBefore(entity => entity.IsBlocker)
        .Sourcefix().Dispatch(0m, MoveMoneySurvey);

      // Conservation locally, not just at the ends: for every entity with children, what went
      // out equals what came in (plus its own contribution). Each edge's amount is recovered by
      // IN-EDGE INDEX -- the honest correlation key; the old payload match was ambiguous under
      // equal weights and is exactly what the library says never to do.
      foreach (var dispatcher in moved.GetEdges().GroupBy(edge => edge.Parent.Node.Name))
      {
        var sentDown = dispatcher.Sum(edge => edge.Child.Arrivals[edge.InEdgeIndex]);

        Assert.AreEqual(
          Received(moved.Values.Single(result => result.Node.Name == dispatcher.Key)),
          sentDown,
          $"conservation at {dispatcher.Key}");
      }
    }

    // ---------------------------------------------------------------------------------------
    // NAV attributes up.
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Attribution_NAV_ConservedThroughTheDiamond()
    {
      // The upward question the structure exists to answer: each fund's NAV by lookthrough,
      // with the shared JV attributed per route -- never double-counted.
      var attributed = ByName(Structure().Sinkfix().Dispatch<decimal>(AttributeUpSurvey));

      decimal Nav(string fund) => attributed[fund].Arrivals.ToArray().Sum();

      Assert.AreEqual(73_000_000m, Nav("FundA"), "SideCo 20M + 60% of OpCo's 80M + BlockedCo 5M");
      Assert.AreEqual(32_000_000m, Nav("FundB"), "40% of OpCo's 80M");

      Assert.AreEqual(
        105_000_000m,
        Nav("FundA") + Nav("FundB"),
        "the funds' NAVs sum to the total holdings -- the diamond did not double-count");
    }

    [TestMethod]
    public void Attribution_AgreesWithTheDownwardLookthrough()
    {
      // The two directions computing the same truth: OpCo's holding times each fund's
      // effective ownership equals what attribution delivers from OpCo's subtree.
      var attributed = Structure().Sinkfix().Dispatch<decimal>(AttributeUpSurvey);

      Assert.AreEqual(
        80_000_000m,
        ByName(attributed)["JV"].Arrivals.ToArray().Sum(),
        "OpCo arrives whole at the JV");

      // FundA's JV-route share: 60% of 80M rode the HoldCo edge.
      CollectionAssert.Contains(UpflowsAt(attributed, "HoldCo").ToList(), (48_000_000m, 0.60m));
      CollectionAssert.Contains(UpflowsAt(attributed, "FundB").ToList(), (32_000_000m, 0.40m));
    }
  }
}
