using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Copse.Linq.Tests
{
  // THE NORTH STAR (ratified 2026-08-05 -- docs/SCANRESULT_DESIGN.md): a scan is the
  // fold-shaped dispatch, so for EVERY boundary flavor,
  //
  //   Scan(boundary, fold)  ==  Dispatch(boundary, (a, dts) => { foreach dt: dt.Dispatch(fold(a, dt.Node)); })
  //
  // Boundary flavors mean the same thing on both tiers -- the two instruments, uniformly:
  // the SEED is the virtual root's arrival, transformed by the tier's callback (fold /
  // survey) so every node participates; the SELECTOR sets each root's value directly,
  // bypassing the callback (known per-root values). This battery pins the invariant that
  // selected the selector's bypass semantics (reversing the one-day arrival-semantics
  // detour); every future boundary flavor must join it.
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

    private static string[] Pairings(ITreenumerable<ScanResult<string, string>> results) =>
      results.PreorderTraversal().Select(pairing => $"{pairing.Node}:{pairing.Accumulate}").ToArray();

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
    public void Leaffix_ScanIsTheFoldShapedDispatch_BySurveyOnlyEncoding()
    {
      // The leaffix half of the invariant: LeaffixScan(map, combine) == the survey-only
      // LeaffixDispatch whose survey starts at the map and folds the children's rollups in --
      // which is literally the scan's implementation, now pinned as law. (Leaffix has no
      // boundary flavors to pair: the map serves every node, and a seed cannot exist where
      // upward flow has no channel for it to participate through.)
      var scan = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest)
          .LeaffixScan(node => node, (accumulate, child) => accumulate + child));

      var dispatch = Pairings(
        TreeSerializer.DeserializeDepthFirstTree(Forest)
          .LeaffixDispatch<string, string>((node, children) =>
          {
            var accumulate = node;
            foreach (var child in children)
              accumulate += child.Accumulate;
            return accumulate;
          }));

      CollectionAssert.AreEqual(new[] { "a:abc", "b:b", "c:c", "d:def", "e:e", "f:f" }, scan);
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
