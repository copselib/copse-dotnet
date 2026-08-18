using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Copse.Linq.Tests
{
  // STAGE 0 of design-docs/PUBLIC_COMPOSITION_SURFACE_DESIGN.md: the behavior-neutrality
  // harness. Each pin asserts the EXACT concrete machine a canonical composition spelling
  // produces today. The reorientation (stages 1-2: the public bases, the reversed
  // hierarchy, the deleted conjunction marker, the public wrapper classes) must be a
  // representation no-op -- every pin here passes UNCHANGED through it, except the
  // Hide-law pin's interface names, which rename mechanically with the interfaces.
  //
  // These are TYPE pins only: behavior is the conformance battery's job. A pin failing
  // after an interface move means the sniffs or a door stopped resolving to the same
  // machine -- exactly the regression class the door-optimality law is meant to prevent.
  [TestClass]
  public class RepresentationPinTests
  {
    private static ITreenumerable<string> Plain()
      => TreeSerializer.DeserializeDepthFirstTree("a(b(c,d),e)");

    private static void AssertMachine(object result, Type openGeneric, string spelling)
      => Assert.AreEqual(
        openGeneric,
        result.GetType().GetGenericTypeDefinition(),
        $"{spelling}: expected {openGeneric.Name}, got {result.GetType().Name}");

    // ---- The light tier: chains stay on the cheapest machinery ----

    [TestMethod]
    public void LightTier_CanonicalSpellings_KeepLightMachines()
    {
      AssertMachine(Plain().Select(n => n + "!"),
        typeof(SelectTreenumerable<,>), "Select");
      AssertMachine(Plain().Select(n => n + "!").Select(text => text.Length),
        typeof(SelectTreenumerable<,>), "Select.Select");
      AssertMachine(Plain().Select((n, position) => $"{n}@{position.Depth}"),
        typeof(SelectTreenumerable<,>), "positional Select");
      AssertMachine(Plain().PruneAfter(n => n == "b"),
        typeof(PruneAfterTreenumerable<>), "PruneAfter");
      AssertMachine(Plain().PruneAfter(n => n == "b").PruneAfter(n => n == "e"),
        typeof(PruneAfterTreenumerable<>), "PruneAfter.PruneAfter");
      AssertMachine(Plain().Select(n => n + "!").PruneAfter(n => n == "b!"),
        typeof(SelectPruneAfterTreenumerable<,>), "Select.PruneAfter");
      AssertMachine(Plain().PruneAfter(n => n == "b").Select(n => n + "!"),
        typeof(SelectPruneAfterTreenumerable<,>), "PruneAfter.Select");
    }

    // ---- The general driver: rejecting operators produce ONE SelectWhere machine ----

    [TestMethod]
    public void GeneralDriver_CanonicalSpellings_ProduceOneMachine()
    {
      AssertMachine(Plain().Where(n => n != "b"),
        typeof(SelectWhereTreenumerable<,,>), "Where");
      AssertMachine(Plain().PruneBefore(n => n == "b"),
        typeof(SelectWhereTreenumerable<,,>), "PruneBefore");
      AssertMachine(
        Plain()
          .Select(n => n + "!")
          .PruneAfter(n => n == "d!")
          .Where(n => n != "b!")
          .PruneBefore(n => n == "e!")
          .Select(n => n.Length),
        typeof(SelectWhereTreenumerable<,,>), "five-operator mix");
    }

    // The join rule's stacking half: a positional lambda after a relabeling operator is
    // entitled to the emitted labels, so it stacks a real light wrapper over the driver.
    [TestMethod]
    public void JoinRule_PositionalSelectAfterRelabeling_StacksALightWrapper()
    {
      AssertMachine(Plain().Where(n => n != "b").Select((n, position) => $"{n}@{position.Depth}"),
        typeof(SelectTreenumerable<,>), "Where then positional Select");
    }

    // ---- The scan citizens: recipe re-plant under Select, the fold-carrying driver
    // under a rejecting operator (the fourth cell) ----

    [TestMethod]
    public void ScanCitizens_CanonicalSpellings_KeepTheirMachines()
    {
      AssertMachine(Plain().RootfixScan(0, (depth, node) => depth + 1),
        typeof(RootfixScanTreenumerable<,>), "RootfixScan");
      AssertMachine(Plain().RootfixScan(0, (depth, node) => depth + 1).Select(pair => pair.Accumulate),
        typeof(RootfixScanProductTreenumerable<,,>), "RootfixScan.Select");
      AssertMachine(
        Plain().RootfixScan(0, (depth, node) => depth + 1).Select(pair => pair.Accumulate).Select(depth => depth * 2),
        typeof(RootfixScanProductTreenumerable<,,>), "RootfixScan.Select.Select");
      AssertMachine(Plain().RootfixScan(0, (depth, node) => depth + 1).Where(pair => pair.Accumulate < 2),
        typeof(ScanWhereTreenumerable<,,,>), "RootfixScan.Where");
      AssertMachine(
        Plain()
          .Select(n => n + "!")
          .RootfixScan(0, (depth, node) => depth + 1)
          .Where(pair => pair.Accumulate < 3)
          .Select(pair => pair.Node),
        typeof(ScanWhereTreenumerable<,,,>), "Select.RootfixScan.Where.Select");
    }

    // ---- The TakeSubtreesWhere citizens: dispatch behind the citizenship ----

    [TestMethod]
    public void TakeSubtreesWhereCitizens_CanonicalSpellings_KeepTheirMachines()
    {
      AssertMachine(Plain().TakeSubtreesWhere(n => n == "b"),
        typeof(TakeSubtreesWhereTreenumerable<>), "TakeSubtreesWhere");
      AssertMachine(Plain().TakeSubtreesWhere(n => n == "b").Select(n => n + "!"),
        typeof(TakeSubtreesWhereProductTreenumerable<,>), "TakeSubtreesWhere.Select");
      AssertMachine(
        Plain().TakeSubtreesWhere(n => n == "b").Select(n => n + "!").Select(text => text.Length),
        typeof(TakeSubtreesWhereProductTreenumerable<,>), "TakeSubtreesWhere.Select.Select");
      AssertMachine(Plain().TakeSubtreesWhere(n => n == "b").Where(n => n != "c"),
        typeof(SelectWhereTreenumerable<,,>), "TakeSubtreesWhere.Where");
      AssertMachine(Plain().TakeSubtreesWhere(n => n == "b").PruneAfter(n => n == "c"),
        typeof(PruneAfterTreenumerable<>), "TakeSubtreesWhere.PruneAfter");
    }

    // ---- The buffer tier: citizenship minted at the Select seam (the thin shape) ----

    [TestMethod]
    public void BufferTier_CanonicalSpellings_KeepTheProjectedCitizen()
    {
      AssertMachine(Plain().Materialize(BufferLayout.Preorder).Select(n => n + "!"),
        typeof(ProjectedTreenumerableBuffer<,>), "Materialize.Select");
      AssertMachine(
        Plain().Materialize(BufferLayout.Preorder).Select(n => n + "!").Select(text => text.Length),
        typeof(ProjectedTreenumerableBuffer<,>), "Materialize.Select.Select");
      AssertMachine(
        Plain()
          .LeaffixScan(leaf => 1, (left, right) => left + right, (accumulate, node) => accumulate + 1)
          .Select(pair => pair.Accumulate),
        typeof(ProjectedTreenumerableBuffer<,>), "LeaffixScan.Select");

      using (var memo = Plain().Memoize())
      {
        AssertMachine(memo.Select(n => n + "!"),
          typeof(ProjectedTreenumerableBuffer<,>), "Memoize.Select");
      }
    }

    // ---- The Hide law: the isolation barrier is the one guaranteed capability-free
    // view. Select over it takes the wrapper fallback; the hidden view itself claims no
    // composition surface. (The interface names in this pin rename mechanically with the
    // stage-1 interfaces; the assertions themselves must hold before and after.) ----

    [TestMethod]
    public void HideLaw_TheHiddenView_ClaimsNoCompositionSurface()
    {
      var hidden = Plain().Select(n => n + "!").Hide();

      Assert.IsFalse(hidden is ISelectTreenumerable<string>, "Hide must strip the Select citizenship");
      Assert.IsFalse(hidden is IPruneAfterTreenumerable<string>, "Hide must strip the PruneAfter citizenship");
      Assert.IsFalse(hidden is ISelectWhereTreenumerable<string>, "Hide must strip the general surface");

      AssertMachine(hidden.Select(n => n.Length),
        typeof(SelectTreenumerable<,>), "Hide.Select takes the wrapper fallback");
    }
  }
}
