using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Phase 3, part A of the categorical audit (docs/CATEGORY_THEORY_SURVEY.md §6): executable
  // verification of the monad laws for SELECTMANY_DESIGN.md's DECIDED semantics -- root-graft
  // substitution (k = 1: children under the expansion root after its own children, the
  // Data.Tree order; k = 0: promotion into the vacated slot, Where's rule; k >= 2: children
  // under the LAST root). The design's implementation note 2 asks for exactly this: the
  // k >= 2 associativity case was chosen for continuity and "asserted, not yet proven."
  //
  // Method: a REFERENCE MODEL (the oracle tradition -- builder + oracle) implements the
  // decided semantics naively over materialized model trees; the laws are checked over a
  // corpus of trees x selectors covering k = 0 / 1 / >= 2 and their interactions (selectors
  // trigger on generated values so nested binds exercise expansion-of-expansion); and the
  // model is GROUNDED against the shipped operators first: bind restricted to
  // {Return, Empty} must reproduce the real Where, and bind of Return-composed must
  // reproduce the real Select -- so the law verdicts are about the design, not about model
  // quirks. The streaming operator remains unbuilt; these are its acceptance criteria.
  [TestClass]
  public class SelectManyLawVerificationTests
  {
    private static readonly string[] Trees =
    {
      "a",
      "a(b(c))",
      "a(b,c)",
      "a,b,c",
      "a(b(d,e),c(f,g))",
      "a(b(d),c)",
      "a,b(d),c(e(f))",
    };

    // ------------------------------------------------------------------- the model

    private sealed class TreeModel
    {
      public TreeModel(string value) { Value = value; Children = new List<TreeModel>(); }
      public string Value { get; }
      public List<TreeModel> Children { get; }
    }

    private static List<TreeModel> ParseForest(string text)
    {
      var position = 0;
      var forest = new List<TreeModel>();

      while (position < text.Length)
      {
        forest.Add(ParseTree(text, ref position));
        if (position < text.Length && text[position] == ',')
          position++;
      }

      return forest;
    }

    private static TreeModel ParseTree(string text, ref int position)
    {
      var start = position;
      while (position < text.Length && text[position] != '(' && text[position] != ')' && text[position] != ',')
        position++;

      var node = new TreeModel(text.Substring(start, position - start));

      if (position < text.Length && text[position] == '(')
      {
        position++; // consume '('
        while (text[position] != ')')
        {
          node.Children.Add(ParseTree(text, ref position));
          if (text[position] == ',')
            position++;
        }
        position++; // consume ')'
      }

      return node;
    }

    private static string Print(List<TreeModel> forest)
      => string.Join(",", forest.Select(PrintTree));

    private static string PrintTree(TreeModel tree)
      => tree.Children.Count == 0 ? tree.Value : $"{tree.Value}({Print(tree.Children)})";

    // The decided bind, reference form. A node's expansion replaces it IN PLACE; its
    // already-rewritten children re-hang under the expansion's LAST root, AFTER that root's
    // own children (k = 1 degenerates to Data.Tree order; k = 0 promotes into the vacated
    // slot -- at the root level, promoted children become roots).
    private static List<TreeModel> BindForest(List<TreeModel> forest, Func<string, List<TreeModel>> selector)
      => forest.SelectMany(tree => BindTree(tree, selector)).ToList();

    private static List<TreeModel> BindTree(TreeModel tree, Func<string, List<TreeModel>> selector)
    {
      var rewrittenChildren = tree.Children.SelectMany(child => BindTree(child, selector)).ToList();
      var expansion = selector(tree.Value);

      if (expansion.Count == 0)
        return rewrittenChildren;                       // k = 0: promotion into the vacated slot

      expansion[expansion.Count - 1].Children.AddRange(rewrittenChildren);   // after its own children
      return expansion;
    }

    private static List<TreeModel> Return(string value) => new List<TreeModel> { new TreeModel(value) };

    // ------------------------------------------------- grounding against shipped operators

    [TestMethod]
    public void Grounding_BindRestrictedToReturnOrEmpty_ReproducesTheRealWhere()
    {
      Func<string, bool> keep = value => value != "b" && value != "e";

      foreach (var tree in Trees)
      {
        var model = Print(BindForest(ParseForest(tree), value => keep(value) ? Return(value) : new List<TreeModel>()));
        var real = TreeSerializer.DeserializeDepthFirstTree(tree).Where(keep).SerializeDepthFirstTree();

        Assert.AreEqual(real, model, $"Where grounding [{tree}]");
      }
    }

    [TestMethod]
    public void Grounding_BindOfReturnComposed_ReproducesTheRealSelect()
    {
      Func<string, string> map = value => value + "!";

      foreach (var tree in Trees)
      {
        var model = Print(BindForest(ParseForest(tree), value => Return(map(value))));
        var real = TreeSerializer.DeserializeDepthFirstTree(tree).Select(map).SerializeDepthFirstTree();

        Assert.AreEqual(real, model, $"Select grounding [{tree}]");
      }
    }

    // ------------------------------------------------------------------- the monad laws

    // Selectors chosen to exercise every k and their interactions: f empties "b" (k = 0),
    // forests "c" (k = 2, second root carrying its own child), and singly expands the rest;
    // g triggers on GENERATED values (suffix-based), so bind-of-bind reaches expansions of
    // expansions, empties inside expansions, and forests inside forests.
    private static List<TreeModel> F(string value)
    {
      if (value == "b") return new List<TreeModel>();
      if (value == "c") return ParseForest("c1,c2(c3)");
      return ParseForest($"{value}1({value}2)");
    }

    private static List<TreeModel> G(string value)
    {
      if (value.EndsWith("2")) return new List<TreeModel>();
      if (value.EndsWith("3") || value == "d1") return ParseForest($"{value}L,{value}R");
      return ParseForest($"{value}x");
    }

    [TestMethod]
    public void MonadLaw_LeftIdentity()
    {
      foreach (var value in new[] { "a", "b", "c", "d" })
        Assert.AreEqual(
          Print(F(value)),
          Print(BindForest(Return(value), F)),
          $"bind(Return({value}), f) ≡ f({value})");
    }

    [TestMethod]
    public void MonadLaw_RightIdentity()
    {
      foreach (var tree in Trees)
        Assert.AreEqual(
          tree,
          Print(BindForest(ParseForest(tree), Return)),
          $"bind(t, Return) ≡ t [{tree}]");
    }

    [TestMethod]
    public void MonadLaw_Associativity_TreeValuedFragment()
    {
      // The k <= 1 fragment: selectors return Empty or a single tree (Return/Empty boundary
      // plus the Data.Tree interior). Rich interactions -- empties on source values AND on
      // generated values, expansion-of-expansion -- across the corpus.
      Func<string, List<TreeModel>> f = value => value == "b" ? new List<TreeModel>() : ParseForest($"{value}1({value}2)");
      Func<string, List<TreeModel>> g = value =>
        value.EndsWith("2") ? new List<TreeModel>()
        : value == "c1" ? new List<TreeModel>()
        : ParseForest($"{value}x({value}y)");

      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), f), g));
        var rightAssociated = Print(BindForest(ParseForest(tree), value => BindForest(f(value), g)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"tree-valued associativity [{tree}]");
      }
    }

    // THE FINDING (2026-08-12, phase 3 part A): the forest-valued (k >= 2) case FAILS
    // associativity -- the exact case SELECTMANY_DESIGN.md's implementation note 2 flagged as
    // "asserted, not yet proven." Pinned here as a documented counterexample.
    //
    // Mechanism: "under the LAST root" is not stable under composition, because a LATER bind
    // can erase the root you attached to. Left-associated, c's children hang under expansion
    // root c2, and the second bind then empties c2 -- promoting them to SIBLINGS of c3's
    // expansion. Right-associated, the composite selector already emptied c2 before
    // attachment, so its last root is c3R and the children hang UNDER c3R:
    //
    //   left:  a1x(d1L, d1R, e1x, c1x, c3L, c3R, f1x, g1x)
    //   right: a1x(d1L, d1R, e1x, c1x, c3L, c3R(f1x, g1x))
    //
    // No fixed-root attachment rule survives downstream erasure; the failure is structural,
    // not a tuning error. The lawful core is the tree-valued fragment above.
    [TestMethod]
    public void Finding_ForestValuedSelectors_BreakAssociativity_TheCounterexample()
    {
      const string tree = "a(b(d,e),c(f,g))";

      var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), F), G));
      var rightAssociated = Print(BindForest(ParseForest(tree), value => BindForest(F(value), G)));

      Assert.AreEqual("a1x(d1L,d1R,e1x,c1x,c3L,c3R,f1x,g1x)", leftAssociated);
      Assert.AreEqual("a1x(d1L,d1R,e1x,c1x,c3L,c3R(f1x,g1x))", rightAssociated);
      Assert.AreNotEqual(leftAssociated, rightAssociated, "the k >= 2 attachment rule is non-associative");
    }

    [TestMethod]
    public void MonadLaw_Associativity_EmptyHeavySelectors()
    {
      // The k = 0 interactions concentrated: f empties interior nodes, g empties expansion
      // roots -- promotion inside promotion, the delicate corner.
      Func<string, List<TreeModel>> emptier = value => value == "b" || value == "c" ? new List<TreeModel>() : Return(value + "'");
      Func<string, List<TreeModel>> rootEater = value => value.EndsWith("'") && value.StartsWith("a") ? new List<TreeModel>() : ParseForest($"{value}z");

      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), emptier), rootEater));
        var rightAssociated = Print(BindForest(ParseForest(tree), value => BindForest(emptier(value), rootEater)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"empty-heavy associativity [{tree}]");
      }
    }
  }
}
