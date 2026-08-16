using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // THE NORTH STAR (ratified 2026-08-05 -- design-docs/SCANRESULT_DESIGN.md): a scan is the
  // fold-shaped dispatch, so for EVERY boundary flavor,
  //
  //   Scan(boundary, fold)  ==  Dispatch(boundary, (a, dts) => { foreach dt: dt.Dispatch(fold(a, dt.Node)); })
  //
  // Boundary flavors mean the same thing on both tiers -- the two instruments, uniformly:
  // the SEED is the virtual root's arrival, transformed by the tier's callback (fold /
  // survey) so every node participates; the SELECTOR sets each boundary node's value
  // directly, bypassing the callback. Since THE VIRTUAL-ROOT RULE (2026-08-06) the
  // quantifier is exact: seeds exist only at the rootfix boundary (the virtual forest root
  // is the family's one tree-lawful virtual node), so every flavor on the surface has a
  // same-boundary twin on the other tier -- no translated equivalences remain. This battery
  // pins the invariant that selected the selector's bypass semantics (reversing the one-day
  // arrival-semantics detour); every future boundary flavor must join it.
  [TestClass]
  public class CrossTierCoherenceTests
  {
    private const string Forest = "a(b,c),d(e,f)";

    private static string Fold(string accumulate, string node) => accumulate + node;

    // The fold, dispatch-encoded: every member receives fold(family arrival, member).
    private static void FoldSurvey(string arrival, DispatchTargets<string, string> members)
    {
      foreach (var member in members)
        member.Dispatch(Fold(arrival, member.Node));
    }

    private static string[] Pairings(ITreenumerable<NodeAccumulation<string, string>> results) =>
      results.GetPreorderTraversal().Select(pairing => $"{pairing.Node}:{pairing.Accumulate}").ToArray();

    // The dispatch side records ARRIVALS (NodeArrival -- the recording rule, type-level since
    // 2026-08-06); under the fold encoding what arrives at a node IS its accumulation, which
    // is exactly the equivalence this battery pins -- the projections coincide by the law.
    private static string[] Pairings(ITreenumerable<NodeArrival<string, string>> results) =>
      results.GetPreorderTraversal().Select(pairing => $"{pairing.Node}:{pairing.Arrival}").ToArray();

    [TestMethod]
    public void SeedFlavor_ScanIsTheFoldShapedDispatch()
    {
      var scan = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixScan("s", Fold));

      var dispatch = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixDispatch("s", FoldSurvey));

      CollectionAssert.AreEqual(new[] { "a:sa", "b:sab", "c:sac", "d:sd", "e:sde", "f:sdf" }, scan,
        "the seed is the virtual root's arrival -- every node transforms through the fold");
      CollectionAssert.AreEqual(scan, dispatch);
    }

    [TestMethod]
    public void SelectorFlavor_ScanIsTheFoldShapedDispatch()
    {
      var scan = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixScan(root => root.ToUpperInvariant(), Fold));

      var dispatch = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixDispatch(root => root.ToUpperInvariant(), FoldSurvey));

      CollectionAssert.AreEqual(new[] { "a:A", "b:Ab", "c:Ac", "d:D", "e:De", "f:Df" }, scan,
        "the selector sets each root's value directly, bypassing the tier's callback");
      CollectionAssert.AreEqual(scan, dispatch);
    }

    [TestMethod]
    public void LeaffixSelectorFlavor_ScanIsTheFoldShapedDispatch()
    {
      var scan = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest)
          .LeaffixScan(leaf => leaf.ToUpperInvariant(), (left, right) => left + right, (accumulate, node) => node + accumulate));

      var dispatch = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest)
          .LeaffixDispatch(
            leaf => leaf.ToUpperInvariant(),
            (node, children) =>
            {
              var reduced = children[0].Accumulate;
              for (var siblingIndex = 1; siblingIndex < children.Count; siblingIndex++)
                reduced += children[siblingIndex].Accumulate;
              return node + reduced;
            }));

      CollectionAssert.AreEqual(new[] { "a:aBC", "b:B", "c:C", "d:dEF", "e:E", "f:F" }, scan,
        "the selector sets each leaf directly, node accumulator bypassed at the fringe");
      CollectionAssert.AreEqual(scan, dispatch);
    }

    [TestMethod]
    public void PositionalSelectorFlavor_ScanIsTheFoldShapedDispatch()
    {
      var scan = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixScan((root, position) => $"[{position.SiblingIndex}]", Fold));

      var dispatch = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest).RootfixDispatch((root, position) => $"[{position.SiblingIndex}]", FoldSurvey));

      CollectionAssert.AreEqual(new[] { "a:[0]", "b:[0]b", "c:[0]c", "d:[1]", "e:[1]e", "f:[1]f" }, scan);
      CollectionAssert.AreEqual(scan, dispatch);
    }
  }
}
