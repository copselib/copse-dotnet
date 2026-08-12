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
  // real Extend (docs/CATEGORY_THEORY_SURVEY.md §4/§6). The focused pair (walkable, handle)
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

    private static IWalkableTreenumerable<string, int> W(string tree)
      => TreeSerializer.DeserializeDepthFirstTree(tree).MaterializeWalkable();

    [TestMethod]
    public void ComonadLaw_ExtendOfExtract_IsIdentity_AndCertifiesTheWalkAdapter()
    {
      foreach (var tree in Trees)
      {
        var walkable = W(tree);
        var extended = walkable.Extend((source, handle) => source.GetValue(handle));

        // Streams: the extended citizen's walk-adapter streams must equal the source's
        // native store-treenumerator streams -- the law and the adapter conformance in one.
        AssertEquivalent(walkable, extended, $"Extend(extract) ≡ id [{tree}]");

        // Adjacency: handles and shape untouched.
        foreach (var handle in walkable.GetHandles())
        {
          Assert.AreEqual(walkable.GetValue(handle), extended.GetValue(handle));
          Assert.AreEqual(walkable.GetParent(handle).HasParent, extended.GetParent(handle).HasParent);
        }
      }
    }

    [TestMethod]
    public void ComonadLaw_ExtractAfterExtend_RecoversTheObserver()
    {
      Func<IWalkableTreenumerable<string, int>, int, string> observer =
        (source, handle) => $"{source.GetValue(handle)}@{Depth(source, handle)}";

      foreach (var tree in Trees)
      {
        var walkable = W(tree);
        var extended = walkable.Extend(observer);

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(observer(walkable, handle), extended.GetValue(handle), $"extract∘extend [{tree}]");
      }
    }

    [TestMethod]
    public void ComonadLaw_CoAssociativity()
    {
      // g: an observation of the source (value plus depth); f: an observation of the
      // g-extended tree (consults the parent's g-value -- a genuinely neighborhood-dependent
      // second observation, so the law is exercised on real co-Kleisli composition).
      Func<IWalkableTreenumerable<string, int>, int, string> g =
        (source, handle) => $"{source.GetValue(handle)}@{Depth(source, handle)}";

      Func<IWalkableTreenumerable<string, int>, int, string> f =
        (source, handle) =>
        {
          var parentResult = source.GetParent(handle);
          var parentLabel = parentResult.HasParent ? source.GetValue(parentResult.Parent) : "⊤";
          return $"{source.GetValue(handle)}<{parentLabel}";
        };

      foreach (var tree in Trees)
      {
        var walkable = W(tree);

        var stepwise = walkable.Extend(g).Extend(f);
        var composed = walkable.Extend((source, handle) => f(source.Extend(g), handle));

        AssertEquivalent(composed, stepwise, $"co-associativity streams [{tree}]");

        foreach (var handle in walkable.GetHandles())
          Assert.AreEqual(composed.GetValue(handle), stepwise.GetValue(handle), $"co-associativity values [{tree}]");
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

      foreach (var tree in Trees)
      {
        var viaScan = TreeSerializer.DeserializeDepthFirstTree(tree)
          .RootfixScan(seed, fold)
          .Select(result => result.Accumulate);

        var viaExtend = W(tree).Extend((source, handle) =>
        {
          var path = new List<string> { source.GetValue(handle) };
          var parentResult = source.GetParent(handle);

          while (parentResult.HasParent)
          {
            path.Add(source.GetValue(parentResult.Parent));
            parentResult = source.GetParent(parentResult.Parent);
          }

          var accumulate = seed;
          for (var index = path.Count - 1; index >= 0; index--)
            accumulate = fold(accumulate, path[index]);

          return accumulate;
        });

        AssertEquivalent(viaScan, viaExtend, $"scan ≡ extend(path fold) [{tree}]");
      }
    }

    // ---------------------------------------------------------------------- helpers

    private static int Depth(IWalkableTreenumerable<string, int> source, int handle)
    {
      var depth = 0;
      var parentResult = source.GetParent(handle);

      while (parentResult.HasParent)
      {
        depth++;
        parentResult = source.GetParent(parentResult.Parent);
      }

      return depth;
    }

    private static void AssertEquivalent<TValue>(
      ITreenumerable<TValue> expected,
      ITreenumerable<TValue> actual,
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

    private static List<(TreenumeratorMode Mode, TValue Node, int VisitCount, NodePosition Position)> DrainVisits<TValue>(
      ITreenumerator<TValue> treenumerator)
    {
      var visits = new List<(TreenumeratorMode, TValue, int, NodePosition)>();

      using (treenumerator)
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          visits.Add((treenumerator.Mode, treenumerator.Node, treenumerator.VisitCount, treenumerator.Position));
      }

      return visits;
    }
  }
}
