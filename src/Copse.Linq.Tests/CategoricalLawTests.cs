using Copse.Core;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Phase 2 of the categorical audit (design-docs/CATEGORY_THEORY_SURVEY.md): the laws each shape
  // owes, pinned SEMANTICALLY -- every equivalence is asserted modulo visit-stream equality
  // (both dimensions), the survey's quotient. Where a law licenses a lattice collapse, the
  // stacked side is forced through Hide (the opaque identity) so the law is tested against
  // the genuinely stacked pipeline, not against the collapse it licenses.
  [TestClass]
  public class CategoricalLawTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a(b(d,e,f),c(g,h,i))",
      "a,b(d),c(e(f))",
    };

    private static ITreenumerable<string> T(string tree) => TreeSerializer.DeserializeDepthFirstTree(tree);

    // ------------------------------------------------------------------ functor laws

    [TestMethod]
    public void Functor_Identity_SelectOfIdentity_IsIdentity()
    {
      foreach (var tree in Trees)
        AssertEquivalent(T(tree), T(tree).Select(value => value), $"Select(id) ≡ id [{tree}]");
    }

    [TestMethod]
    public void Functor_Composition_StackedSelects_EqualComposedSelect()
    {
      Func<string, string> f = value => value + "!";
      Func<string, string> g = value => value.ToUpperInvariant();

      foreach (var tree in Trees)
      {
        // The lattice-collapsed spelling...
        AssertEquivalent(T(tree).Select(value => g(f(value))), T(tree).Select(f).Select(g), $"collapsed [{tree}]");

        // ...and the law itself, with the stacked side FORCED via the opaque identity.
        AssertEquivalent(T(tree).Select(value => g(f(value))), T(tree).Select(f).Hide().Select(g), $"forced-stacked [{tree}]");
      }
    }

    // ------------------------------------------------------------- licensing squares

    [TestMethod]
    public void Licensing_WherePredicateMerge_StackedWheres_EqualConjunction()
    {
      Func<string, bool> p = value => value != "b";
      Func<string, bool> q = value => value != "c";

      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree).Where(value => p(value) && q(value)),
          T(tree).Where(p).Hide().Where(q),
          $"Where merge [{tree}]");
    }

    [TestMethod]
    public void Licensing_SelectWhereInterchange()
    {
      Func<string, string> f = value => value + "!";
      Func<string, bool> mappedPredicate = value => value != "b!";

      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree).Where(value => mappedPredicate(f(value))).Select(f),
          T(tree).Select(f).Hide().Where(mappedPredicate),
          $"Select/Where interchange [{tree}]");
    }

    [TestMethod]
    public void Licensing_PruneDescendantsWhereMerge_StackedPrunes_EqualDisjunction()
    {
      Func<string, bool> p = value => value == "b";
      Func<string, bool> q = value => value == "c";

      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree).PruneDescendantsWhere(value => p(value) || q(value)),
          T(tree).PruneDescendantsWhere(p).Hide().PruneDescendantsWhere(q),
          $"PruneDescendantsWhere merge [{tree}]");
    }

    [TestMethod]
    public void Licensing_PruneSubtreesWhereMerge_StackedPrunes_EqualDisjunction()
    {
      Func<string, bool> p = value => value == "b";
      Func<string, bool> q = value => value == "c";

      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree).PruneSubtreesWhere(value => p(value) || q(value)),
          T(tree).PruneSubtreesWhere(p).Hide().PruneSubtreesWhere(q),
          $"PruneSubtreesWhere merge [{tree}]");
    }

    // -------------------------------------------------------- natural transformation

    [TestMethod]
    public void Invert_Involution_DoubleMirror_IsIdentity()
    {
      foreach (var tree in Trees)
        AssertEquivalent(T(tree), T(tree).Invert().Invert(), $"Invert∘Invert ≡ id [{tree}]");
    }

    [TestMethod]
    public void Invert_Naturality_CommutesWithSelect()
    {
      Func<string, string> f = value => value + "!";

      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree).Invert().Select(f),
          T(tree).Select(f).Invert(),
          $"Invert naturality [{tree}]");
    }

    // ------------------------------------------------------------- zip / monoid laws

    [TestMethod]
    public void Union_EmptyIsLeftIdentity()
    {
      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree),
          Tree.Empty<string>().Union(T(tree)).Select(merged => merged.Right),
          $"Empty ∪ t ≡ t [{tree}]");
    }

    [TestMethod]
    public void Union_EmptyIsRightIdentity()
    {
      foreach (var tree in Trees)
        AssertEquivalent(
          T(tree),
          T(tree).Union(Tree.Empty<string>()).Select(merged => merged.Left),
          $"t ∪ Empty ≡ t [{tree}]");
    }

    [TestMethod]
    public void Union_Associativity_UpToReassociation()
    {
      // The three-way merge, both associations, flattened to one canonical description per
      // node: which of the three sides are present, and their values.
      foreach (var left in Trees)
      foreach (var middle in new[] { "a(b)", "a,x", "a(b(c),z)" })
      foreach (var right in new[] { "a(q)", "w" })
      {
        var leftAssociated = T(left).Union(T(middle)).Union(T(right))
          .Select(outer => Canonical(
            outer.HasLeft && outer.Left.HasLeft, outer.HasLeft ? outer.Left.Left : null,
            outer.HasLeft && outer.Left.HasRight, outer.HasLeft ? outer.Left.Right : null,
            outer.HasRight, outer.Right));

        var rightAssociated = T(left).Union(T(middle).Union(T(right)))
          .Select(outer => Canonical(
            outer.HasLeft, outer.Left,
            outer.HasRight && outer.Right.HasLeft, outer.HasRight ? outer.Right.Left : null,
            outer.HasRight && outer.Right.HasRight, outer.HasRight ? outer.Right.Right : null));

        AssertEquivalent(leftAssociated, rightAssociated, $"∪ associativity [{left} | {middle} | {right}]");
      }
    }

    [TestMethod]
    public void Intersection_EmptyAnnihilates()
    {
      foreach (var tree in Trees)
      {
        Assert.AreEqual(0, CountVisits(T(tree).Intersection(Tree.Empty<string>())), $"t ∩ Empty [{tree}]");
        Assert.AreEqual(0, CountVisits(Tree.Empty<string>().Intersection(T(tree))), $"Empty ∩ t [{tree}]");
      }
    }

    [TestMethod]
    public void Subtract_EmptyIsRightIdentity_AndLeftAnnihilator()
    {
      foreach (var tree in Trees)
      {
        AssertEquivalent(T(tree), T(tree).Subtract(Tree.Empty<string>()), $"t − Empty ≡ t [{tree}]");
        Assert.AreEqual(0, CountVisits(Tree.Empty<string>().Subtract(T(tree))), $"Empty − t [{tree}]");
      }
    }

    [TestMethod]
    public void Union_Commutativity_UpToSwap()
    {
      foreach (var left in Trees)
      foreach (var right in new[] { "a(b)", "a,x", "a(b(c),z)" })
        AssertEquivalent(
          T(left).Union(T(right)).Select(merged => CanonicalPair(merged.HasLeft, merged.Left, merged.HasRight, merged.Right)),
          T(right).Union(T(left)).Select(merged => CanonicalPair(merged.HasRight, merged.Right, merged.HasLeft, merged.Left)),
          $"∪ commutativity [{left} | {right}]");
    }

    [TestMethod]
    public void Intersection_Commutativity_UpToSwap()
    {
      foreach (var left in Trees)
      foreach (var right in new[] { "a(b)", "a,x", "a(b(c),z)" })
        AssertEquivalent(
          T(left).Intersection(T(right)).Select(merged => CanonicalPair(merged.HasLeft, merged.Left, merged.HasRight, merged.Right)),
          T(right).Intersection(T(left)).Select(merged => CanonicalPair(merged.HasRight, merged.Right, merged.HasLeft, merged.Left)),
          $"∩ commutativity [{left} | {right}]");
    }

    [TestMethod]
    public void SymmetricDifference_EmptyIsIdentity_BothSides()
    {
      foreach (var tree in Trees)
      {
        AssertEquivalent(
          T(tree),
          T(tree).SymmetricDifference(Tree.Empty<string>()).Select(merged => merged.Left),
          $"t Δ Empty ≡ t [{tree}]");

        AssertEquivalent(
          T(tree),
          Tree.Empty<string>().SymmetricDifference(T(tree)).Select(merged => merged.Right),
          $"Empty Δ t ≡ t [{tree}]");
      }
    }

    [TestMethod]
    public void SymmetricDifference_Commutativity_UpToSwap()
    {
      foreach (var left in Trees)
      foreach (var right in new[] { "a(b)", "a,x", "a(b(c),z)" })
        AssertEquivalent(
          T(left).SymmetricDifference(T(right)).Select(merged => CanonicalPair(merged.HasLeft, merged.Left, merged.HasRight, merged.Right)),
          T(right).SymmetricDifference(T(left)).Select(merged => CanonicalPair(merged.HasRight, merged.Right, merged.HasLeft, merged.Left)),
          $"Δ commutativity [{left} | {right}]");
    }

    // Deliberately ABSENT: SymmetricDifference associativity. Tree-Δ is Union.Where(!both) --
    // the PROMOTE reshaping rule -- and promotion shifts positions, so the set-theoretic xor
    // law does not transfer to positional merges. A documented non-law, like positional
    // Where's non-composition (see the survey's SymmetricDifference row).

    // ------------------------------------------------------------------ effect laws

    [TestMethod]
    public void Do_OfNoop_IsIdentity()
    {
      foreach (var tree in Trees)
        AssertEquivalent(T(tree), T(tree).Do(_ => { }), $"Do(noop) ≡ id [{tree}]");
    }

    [TestMethod]
    public void Do_AdjacentObserversMerge_StreamsAndTracesAgree()
    {
      foreach (var tree in Trees)
      {
        var stackedTrace = new List<string>();
        var mergedTrace = new List<string>();

        var stacked = T(tree)
          .Do(visit => stackedTrace.Add($"a:{visit.Node}:{visit.Mode}"))
          .Do(visit => stackedTrace.Add($"b:{visit.Node}:{visit.Mode}"));

        var merged = T(tree)
          .Do(visit =>
          {
            mergedTrace.Add($"a:{visit.Node}:{visit.Mode}");
            mergedTrace.Add($"b:{visit.Node}:{visit.Mode}");
          });

        // Streams agree (the coarse quotient)...
        AssertEquivalent(merged, stacked, $"Do merge streams [{tree}]");

        // ...and, in the finer setting Do lives in, the effect TRACES agree too. The stream
        // comparison above drained both pipelines twice (DFT + BFT), so both traces carry
        // both drains -- per-drain effects, the re-enumeration contract, in the same order.
        CollectionAssert.AreEqual(mergedTrace, stackedTrace, $"Do merge traces [{tree}]");
      }
    }

    // ----------------------------------------------------------- embedding functors

    [TestMethod]
    public void Embeddings_AreFunctorial()
    {
      var items = new[] { "a", "b", "c", "d" };
      Func<string, string> f = value => value + "!";

      AssertEquivalent(
        items.Select(f).ToDegenerateTree(),
        items.ToDegenerateTree().Select(f),
        "ToDegenerateTree functoriality");

      AssertEquivalent(
        items.Select(f).ToTrivialForest(),
        items.ToTrivialForest().Select(f),
        "ToTrivialForest functoriality");
    }

    // ---------------------------------------------------------------------- helpers

    private static string Canonical(
      bool hasFirst, string first, bool hasSecond, string second, bool hasThird, string third)
      => $"{(hasFirst ? first : "∅")}|{(hasSecond ? second : "∅")}|{(hasThird ? third : "∅")}";

    private static string CanonicalPair(bool hasFirst, string first, bool hasSecond, string second)
      => $"{(hasFirst ? first : "∅")}|{(hasSecond ? second : "∅")}";

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

    private static int CountVisits<TNode>(ITreenumerable<TNode> tree)
      => DrainVisits(tree.GetDepthFirstTreenumerator()).Count;

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
