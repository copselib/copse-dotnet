using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Phase 3, part B of the categorical audit: the Store comonad's laws, pinned against the
  // real Extend (design-docs/CATEGORY_THEORY_SURVEY.md §4/§6). The focused pair (walkable, handle)
  // is the comonad; extract = GetValue; Extend = the neighborhood-aware relabel. The first
  // law doubles as the Walk adapter's conformance pin: Extend(extract) streams the source's
  // own visit streams through the engine-driven adapter, so equality certifies the adapter
  // against the store treenumerators (the degenerate-tower pin). The suite closes with the
  // scan-coherence law the survey promised: the scan tier IS extend restricted to
  // order-factoring folds.
  [TestClass]
  public class WalkerComonadLawTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a,b(d),c(e(f))",
    };

    private static IEnumerable<(string Tree, IWalkableTreenumerable<string, int> Walkable)> AllWalkables()
      => Trees.SelectMany(tree => WalkerLawProviders.Walkables(tree).Select(walkable => (tree, walkable)));

    [TestMethod]
    public void ComonadLaw_ExtendOfExtract_IsIdentity_AndCertifiesTheWalkAdapter()
    {
      foreach (var (tree, walkable) in AllWalkables())
      {
        var extended = walkable.Extend((source, handle) => WalkerLawProviders.TopologyOf(source).GetValue(handle));

        // Streams: the extended citizen's walk-adapter streams must equal the source's
        // native store-treenumerator streams -- the law and the adapter conformance in one.
        AssertEquivalent(walkable, extended, $"Extend(extract) ≡ id [{tree}]");

        // Adjacency: handles and shape untouched.
        foreach (var handle in walkable.GetHandles())
        {
          Assert.AreEqual(WalkerLawProviders.TopologyOf(walkable).GetValue(handle), WalkerLawProviders.TopologyOf(extended).GetValue(handle));
          Assert.AreEqual(WalkerLawProviders.TopologyOf(walkable).TryGetParent(handle).HasValue, WalkerLawProviders.TopologyOf(extended).TryGetParent(handle).HasValue);
        }
      }
    }

    [TestMethod]
    public void ComonadLaw_ExtractAfterExtend_RecoversTheObserver()
    {
      Func<ITreeTopology<string, int>, int, string> observer =
        (source, handle) => $"{WalkerLawProviders.TopologyOf(source).GetValue(handle)}@{Depth(source, handle)}";

      foreach (var (tree, walkable) in AllWalkables())
      {
        var extended = walkable.Extend(observer);

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(observer(WalkerLawProviders.TopologyOf(walkable), handle), WalkerLawProviders.TopologyOf(extended).GetValue(handle), $"extract∘extend [{tree}]");
      }
    }

    [TestMethod]
    public void ComonadLaw_CoAssociativity()
    {
      // g: an observation of the source (value plus depth); f: an observation of the
      // g-extended tree (consults the parent's g-value -- a genuinely neighborhood-dependent
      // second observation, so the law is exercised on real co-Kleisli composition).
      Func<ITreeTopology<string, int>, int, string> g =
        (source, handle) => $"{WalkerLawProviders.TopologyOf(source).GetValue(handle)}@{Depth(source, handle)}";

      Func<ITreeTopology<string, int>, int, string> f =
        (source, handle) =>
        {
          var parentResult = WalkerLawProviders.TopologyOf(source).TryGetParent(handle);
          var parentLabel = parentResult.HasValue ? WalkerLawProviders.TopologyOf(source).GetValue(parentResult.Value) : "⊤";
          return $"{WalkerLawProviders.TopologyOf(source).GetValue(handle)}<{parentLabel}";
        };

      foreach (var (tree, walkable) in AllWalkables())
      {

        var stepwise = walkable.Extend(g).Extend(f);
        var composed = walkable.Extend((source, handle) => f(WalkerLawProviders.TopologyOf(source.Extend(g)), handle));

        AssertEquivalent(composed, stepwise, $"co-associativity streams [{tree}]");

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(WalkerLawProviders.TopologyOf(composed).GetValue(handle), WalkerLawProviders.TopologyOf(stepwise).GetValue(handle), $"co-associativity values [{tree}]");
      }
    }

    // The survey's promised coherence law: the scan tier is extend restricted to observations
    // that factor through a fold along the traversal order. RootfixScan's accumulation at
    // every node equals Extend of the root-path fold -- the streaming tier's cross-tenant
    // meets the true comonadic operation, and they agree.
    [TestMethod]
    public void Coherence_RootfixScan_IsExtendOfTheRootPathFold()
    {
      const string seed = "•";
      Func<string, string, string> fold = (accumulate, value) => accumulate + value;

      foreach (var (tree, walkable) in AllWalkables())
      {
        var viaScan = TreeSerializer.DeserializeDepthFirstTree(tree)
          .RootfixScan(seed, fold)
          .Select(result => result.Accumulate);

        var viaExtend = walkable.Extend((source, handle) =>
        {
          var path = new List<string> { WalkerLawProviders.TopologyOf(source).GetValue(handle) };
          var parentResult = WalkerLawProviders.TopologyOf(source).TryGetParent(handle);

          while (parentResult.HasValue)
          {
            path.Add(WalkerLawProviders.TopologyOf(source).GetValue(parentResult.Value));
            parentResult = WalkerLawProviders.TopologyOf(source).TryGetParent(parentResult.Value);
          }

          var accumulate = seed;
          for (var index = path.Count - 1; index >= 0; index--)
            accumulate = fold(accumulate, path[index]);

          return accumulate;
        });

        AssertEquivalent(viaScan, viaExtend, $"scan ≡ extend(path fold) [{tree}]");
      }
    }

    // The upward twin, completing the pair: LeaffixScan is extend of the SUBTREE fold
    // (synthesized attributes, where the rootfix coherence above is the inherited ones).
    // The decided leaffix shape: value(n) = nodeAcc(edgeReduce(children's accumulations), n),
    // edgeReduce a left-fold from the first child. The fringe is selector-only (the
    // virtual-root rule, 2026-08-06), so the seed rides the formula spelling:
    // leaf => nodeAcc(seed, leaf) -- the fold side of the pin keeps the identical leaf case.
    [TestMethod]
    public void Coherence_LeaffixScan_IsExtendOfTheSubtreeFold()
    {
      const string seed = "•";
      Func<string, string, string> edgeAccumulator = (left, right) => left + "|" + right;
      Func<string, string, string> nodeAccumulator = (accumulate, value) => accumulate + value;

      foreach (var (tree, walkable) in AllWalkables())
      {
        var viaScan = TreeSerializer.DeserializeDepthFirstTree(tree)
          .LeaffixScan(leaf => nodeAccumulator(seed, leaf), edgeAccumulator, nodeAccumulator)
          .Select(result => result.Accumulate);

        var viaExtend = walkable.Extend((source, handle) => SubtreeFold(source, handle, seed, edgeAccumulator, nodeAccumulator));

        AssertEquivalent(viaScan, viaExtend, $"leaffix ≡ extend(subtree fold) [{tree}]");
      }
    }

    private static string SubtreeFold(
      ITreeTopology<string, int> source,
      int handle,
      string seed,
      Func<string, string, string> edgeAccumulator,
      Func<string, string, string> nodeAccumulator)
    {
      var childAccumulations = new List<string>();

      for (var childIndex = 0; ; childIndex++)
      {
        var childResult = WalkerLawProviders.TopologyOf(source).TryGetChildAt(handle, childIndex);

        if (!childResult.HasValue)
          break;

        childAccumulations.Add(SubtreeFold(source, childResult.Value.Handle, seed, edgeAccumulator, nodeAccumulator));
      }

      if (childAccumulations.Count == 0)
        return nodeAccumulator(seed, WalkerLawProviders.TopologyOf(source).GetValue(handle));

      var reduced = childAccumulations[0];
      for (var siblingIndex = 1; siblingIndex < childAccumulations.Count; siblingIndex++)
        reduced = edgeAccumulator(reduced, childAccumulations[siblingIndex]);

      return nodeAccumulator(reduced, WalkerLawProviders.TopologyOf(source).GetValue(handle));
    }

    // ---------------------------------------------------------------------- helpers

    private static int Depth(ITreeTopology<string, int> source, int handle)
    {
      var depth = 0;
      var parentResult = WalkerLawProviders.TopologyOf(source).TryGetParent(handle);

      while (parentResult.HasValue)
      {
        depth++;
        parentResult = WalkerLawProviders.TopologyOf(source).TryGetParent(parentResult.Value);
      }

      return depth;
    }

    private static void AssertEquivalent<TNode>(
      ITreenumerable<TNode> expected,
      ITreenumerable<TNode> actual,
      string law)
    {
      CollectionAssert.AreEqual(
        DrainVisits(expected.GetDepthFirstTreenumerator()),
        DrainVisits(actual.GetDepthFirstTreenumerator()),
        $"{law} (depth-first)");

      CollectionAssert.AreEqual(
        DrainVisits(expected.GetBreadthFirstTreenumerator()),
        DrainVisits(actual.GetBreadthFirstTreenumerator()),
        $"{law} (breadth-first)");
    }

    private static List<(TreenumeratorMode Mode, TNode Node, int VisitCount, NodePosition Position)> DrainVisits<TNode>(
      ITreenumerator<TNode> treenumerator)
    {
      var visits = new List<(TreenumeratorMode, TNode, int, NodePosition)>();

      using (treenumerator)
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add((treenumerator.Mode, treenumerator.Node, treenumerator.VisitCount, treenumerator.Position));
      }

      return visits;
    }
  }
}
