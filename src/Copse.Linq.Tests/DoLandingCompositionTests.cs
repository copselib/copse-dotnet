using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // THE LANDING COMPOSITION (the Do-quartet demotion record, 2026-08-04 --
  // docs/SCANRESULT_DESIGN.md): mutable-node workloads are served by composition, not by
  // dedicated operators:
  //
  //   pure aggregation  .  Do(scheduling-filtered landing)  .  Select(.Node)
  //
  // The scheduling-mode filter is load-bearing: Do fires per VISIT EVENT (a k-child node
  // emits 1 S + k+1 V), and scheduling alone is once per node. Effects fire PER DRAIN -- the
  // re-enumeration contract; ITreenumerable is a promise to re-enumerate -- and the consumer
  // who wants exactly-once pins with Materialize/Memoize (a Materialize is definitionally one
  // full traversal, so partial-effect and double-effect hazards vanish behind the pin). A
  // dedicated once operator could not exist without choosing Materialize vs Memoize for the
  // consumer; the recipe is the API. This battery pins the idiom for all four aggregation
  // shapes plus the effect-count semantics.
  [TestClass]
  public class DoLandingCompositionTests
  {
    private sealed class Entity
    {
      public string Name;
      public decimal Weight;
      public decimal Landed;

      public override string ToString() => Name;
    }

    // a-10(b-5(d-1,e-2),c-4): weights on every node.
    private static ITreenumerableBuffer<Entity> Corpus() =>
      TreeSerializer
        .DeserializeDepthFirstTree("a-10(b-5(d-1,e-2),c-4)", (string s) =>
        {
          var parts = s.Split('-');
          return new Entity { Name = parts[0], Weight = decimal.Parse(parts[1]) };
        })
        .Materialize();

    private static decimal[] LandedPreorder(ITreenumerable<Entity> corpus) =>
      corpus.PreorderTraversal().Select(e => e.Landed).ToArray();

    // The canonical landing effect: once per node, at scheduling.
    private static System.Action<NodeVisit<ScanResult<Entity, decimal>>> Land() =>
      visit =>
      {
        if (visit.Mode == TreenumeratorMode.SchedulingNode)
          visit.Node.Node.Landed = visit.Node.Accumulate;
      };

    [TestMethod]
    public void RootfixScanLanding_SeedFlavor()
    {
      var corpus = Corpus();

      corpus
        .RootfixScan(100m, (arrived, e) => arrived + e.Weight)
        .Do(Land())
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { 110m, 115m, 116m, 117m, 114m }, LandedPreorder(corpus));
    }

    [TestMethod]
    public void RootfixScanLanding_SelectorFlavor_RootsLandTheSelectorValue()
    {
      // The bypass instrument (THE NORTH STAR, 2026-08-05): the selector sets the root's
      // accumulation directly -- the root lands 100 verbatim, the fold fires from there.
      var corpus = Corpus();

      corpus
        .RootfixScan(root => 100m, (arrived, e) => arrived + e.Weight)
        .Do(Land())
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { 100m, 105m, 106m, 107m, 104m }, LandedPreorder(corpus),
        "roots land the selector's return directly; children fold from it");
    }

    [TestMethod]
    public void LeaffixScanLanding()
    {
      var corpus = Corpus();

      corpus
        .LeaffixScan(0m, (left, right) => left + right, (accumulate, e) => accumulate + e.Weight)
        .Do(Land())
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      // Subtree sums: d=1, e=2, b=5+1+2=8, c=4, a=10+8+4=22.
      CollectionAssert.AreEqual(new[] { 22m, 8m, 1m, 2m, 4m }, LandedPreorder(corpus));
    }

    // The work-shaped survey (subject-less, the unified signature): allocate the family's
    // arrival pro rata by member weight -- the virtual root family included.
    private static void AllocateByWeight(decimal arrival, DispatchTargets<Entity, decimal> members)
    {
      var totalWeight = 0m;
      foreach (var member in members)
        totalWeight += member.Node.Weight;

      foreach (var member in members)
        member.Dispatch(arrival * member.Node.Weight / totalWeight);
    }

    [TestMethod]
    public void RootfixDispatchLanding_TheWorkAllocator()
    {
      var corpus = Corpus();

      corpus
        .RootfixDispatch(9_000m, AllocateByWeight)
        .Do(Land())
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      // The virtual family hands the sole root the whole budget; b/c split 9000 by 5:4;
      // d/e split b's 5000 by 1:2.
      CollectionAssert.AreEqual(
        new[] { 9_000m, 5_000m, 5_000m / 3, 5_000m * 2 / 3, 4_000m }, LandedPreorder(corpus));
    }

    // The upward survey: a node's accumulation is its weight plus its children's rollups --
    // full participation, so it answers for leaves too (empty sources).
    private static decimal RollUp(Entity node, DispatchSources<Entity, decimal> children)
    {
      var total = node.Weight;
      foreach (var child in children)
        total += child.Accumulate;

      return total;
    }

    [TestMethod]
    public void LeaffixDispatchLanding_SurveyOnly()
    {
      var corpus = Corpus();

      corpus
        .LeaffixDispatch<Entity, decimal>(RollUp)
        .Do(Land())
        .Select(pairing => pairing.Node)
        .PreorderTraversal().ToArray();

      CollectionAssert.AreEqual(new[] { 22m, 8m, 1m, 2m, 4m }, LandedPreorder(corpus));
    }

    [TestMethod]
    public void ImpureSelectIsTheTrap_TheEffectCountDependsOnTheConsumer()
    {
      // The tempting "safer" recipe -- landing inside Select -- is the trap (found in the
      // field, 2026-08-04): the wrapper projects per PULLED VISIT, so an impure selector's
      // effect count depends on the CONSUMER's pull pattern -- a value drain pulls
      // scheduling-only and LOOKS once-per-node; a structural drain pulls the full visit
      // stream and re-projects per visit -- and on downstream COMPOSITION (a following Where
      // fuses to once per tested node; CompositionTests). Contractually unspecified on every
      // axis. Idempotent assignments hide the variability; += compounds. Effects belong in
      // Do, whose scheduling filter is deterministic under every consumer and composition.
      var corpus = Corpus();
      var selectorCalls = 0;

      var landing = corpus
        .RootfixScan(100m, (arrived, e) => arrived + e.Weight)
        .Select(pairing => { selectorCalls++; return pairing.Node; });  // impure: the anti-pattern

      landing.PreorderTraversal().ToArray();
      var valueDrainCalls = selectorCalls;

      selectorCalls = 0;
      landing.ToFormattedString();
      var structuralDrainCalls = selectorCalls;

      Assert.AreEqual(5, valueDrainCalls,
        "a value drain pulls scheduling-only -- deceptively once per node; this is why the recipe LOOKS safe");
      Assert.IsTrue(structuralDrainCalls > valueDrainCalls,
        $"a structural drain re-projects per visit ({structuralDrainCalls} calls, 5 nodes) -- same chain, different consumer, different effect count");
    }

    [TestMethod]
    public void EffectsFirePerDrain_AndMaterializeIsTheConsumersPin()
    {
      // The re-enumeration contract: the landing chain refires per drain (each drain of each
      // dimension is a traversal), and the once-recipe is the consumer's Materialize -- one
      // full traversal at the capture, replays never re-fire. A dedicated DoOnce could not
      // choose Materialize vs Memoize for the consumer; the recipe is the API.
      var corpus = Corpus();
      var landings = 0;

      var landing = corpus
        .RootfixScan(100m, (arrived, e) => arrived + e.Weight)
        .Do(visit =>
        {
          if (visit.Mode == TreenumeratorMode.SchedulingNode)
            landings++;
        })
        .Select(pairing => pairing.Node);

      landing.PreorderTraversal().ToArray();
      landing.LevelOrderTraversal().ToArray();
      Assert.AreEqual(10, landings, "per drain: two traversals of five nodes each");

      landings = 0;
      var pinned = landing.Materialize();
      pinned.PreorderTraversal().ToArray();
      pinned.LevelOrderTraversal().ToArray();
      pinned.PreorderTraversal().ToArray();
      Assert.AreEqual(5, landings, "the pin: one full traversal at the capture, replays are replays");
    }
  }
}
