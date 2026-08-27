using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using static Copse.Linq.Tests.PointedBindReferenceModel;

namespace Copse.Linq.Tests
{
  // SELECTMANY_DESIGN.md's option 2, made precise by the sentinel completion
  // (CATEGORY_THEORY_SURVEY.md §12) and verified here: the 2026-08-12 finding was that no
  // FIXED-root attachment rule survives composition, because a later bind can erase the
  // root you attached to -- the same arity-one theorem that forced the unfocused stance
  // (a forest has zero-or-many roots; nothing focused can stand for it). The repair is to
  // let the SELECTOR designate the attachment stance: a POINTED expansion -- the phantom
  // child of PointedBindReferenceModel, whose inheritance under later binds is bind's own
  // child-handling (erasure of its parent promotes it, position preserved).
  //
  // Spellings: Return(v) = v(slot); Empty = the slot alone (promotion falls out); k >= 2 =
  // the caller says where, including between roots; a SLOTLESS forest drops the children
  // (the vanish rule -- PruneSubtreesWhere as slotless-empty, PruneDescendantsWhere as slotless-leaf). Bind's
  // OUTPUT is a plain forest: the point is consumed at attachment.
  //
  // Same reference-model method as SelectManyLawVerificationTests (whose model this
  // extends): grounded against shipped operators first, then the laws -- including the
  // exact selectors of the pinned k >= 2 counterexample, which this rule must reconcile.
  [TestClass]
  public class PointedSelectManyLawVerificationTests
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

    // ------------------------------------------------- grounding against shipped operators

    [TestMethod]
    public void Grounding_BindRestrictedToReturnOrEmpty_ReproducesTheRealWhere()
    {
      Func<string, bool> keep = value => value != "b" && value != "e";

      foreach (var tree in Trees)
      {
        var model = Print(BindForest(ParseForest(tree), value => keep(value) ? ReturnPointed(value) : EmptyPointed()));
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
        var model = Print(BindForest(ParseForest(tree), value => ReturnPointed(map(value))));
        var real = TreeSerializer.DeserializeDepthFirstTree(tree).Select(map).SerializeDepthFirstTree();

        Assert.AreEqual(real, model, $"Select grounding [{tree}]");
      }
    }

    [TestMethod]
    public void Grounding_SlotlessEmpty_ReproducesTheRealPruneSubtreesWhere()
    {
      Func<string, bool> prune = value => value == "b" || value == "e";

      foreach (var tree in Trees)
      {
        var model = Print(BindForest(ParseForest(tree), value => prune(value) ? SlotlessEmpty() : ReturnPointed(value)));
        var real = TreeSerializer.DeserializeDepthFirstTree(tree).PruneSubtreesWhere(prune).SerializeDepthFirstTree();

        Assert.AreEqual(real, model, $"PruneSubtreesWhere grounding [{tree}]");
      }
    }

    [TestMethod]
    public void Grounding_SlotlessLeaf_ReproducesTheRealPruneDescendantsWhere()
    {
      Func<string, bool> prune = value => value == "b" || value == "c";

      foreach (var tree in Trees)
      {
        var model = Print(BindForest(ParseForest(tree), value => prune(value) ? SlotlessLeaf(value) : ReturnPointed(value)));
        var real = TreeSerializer.DeserializeDepthFirstTree(tree).PruneDescendantsWhere(prune).SerializeDepthFirstTree();

        Assert.AreEqual(real, model, $"PruneDescendantsWhere grounding [{tree}]");
      }
    }

    // ------------------------------------------------------------------- the monad laws

    // The 2026-08-12 counterexample's selectors, pointed to mirror their original intent
    // (attachment under the last root, after its own children) -- the exact case the fixed
    // rule failed.
    private static List<TreeModel> F(string value)
    {
      if (value == "b") return EmptyPointed();
      if (value == "c") return ParseForest($"c1,c2(c3,{Slot})");
      return ParseForest($"{value}1({value}2,{Slot})");
    }

    private static List<TreeModel> G(string value)
    {
      if (value.EndsWith("2")) return EmptyPointed();
      if (value.EndsWith("3") || value == "d1") return ParseForest($"{value}L,{value}R({Slot})");
      return ParseForest($"{value}x({Slot})");
    }

    // The same selectors with DIFFERENT slot placements -- first root, between roots --
    // because the law must hold for every placement the caller can spell.
    private static List<TreeModel> FFirstRoot(string value)
    {
      if (value == "b") return EmptyPointed();
      if (value == "c") return ParseForest($"c1({Slot}),c2(c3)");
      return ParseForest($"{value}1({Slot},{value}2)");
    }

    private static List<TreeModel> GBetweenRoots(string value)
    {
      if (value.EndsWith("2")) return EmptyPointed();
      if (value.EndsWith("3") || value == "d1") return ParseForest($"{value}L,{Slot},{value}R");
      return ParseForest($"{value}x({Slot})");
    }

    [TestMethod]
    public void MonadLaw_LeftIdentity()
    {
      // bind(Return(v), f) ≡ f(v) with its point consumed (bind's output is plain).
      foreach (var value in new[] { "a", "b", "c", "d" })
      {
        var expected = F(value);
        SpliceAtSlot(expected, new List<TreeModel>());

        Assert.AreEqual(Print(expected), Print(BindForest(ParseForest(value), F)), $"bind(Return({value}), f) ≡ f({value})");
      }
    }

    [TestMethod]
    public void MonadLaw_RightIdentity()
    {
      foreach (var tree in Trees)
        Assert.AreEqual(tree, Print(BindForest(ParseForest(tree), ReturnPointed)), $"bind(t, Return) ≡ t [{tree}]");
    }

    [TestMethod]
    public void MonadLaw_Associativity_TheFormerCounterexampleSelectors()
    {
      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), F), G));
        var rightAssociated = Print(BindForest(ParseForest(tree), Compose(F, G)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"pointed associativity, counterexample selectors [{tree}]");
      }
    }

    [TestMethod]
    public void MonadLaw_Associativity_AcrossSlotPlacements()
    {
      foreach (var tree in Trees)
      {
        foreach (var (f, g, label) in new (Func<string, List<TreeModel>>, Func<string, List<TreeModel>>, string)[]
        {
          (FFirstRoot, G, "first-root f"),
          (F, GBetweenRoots, "between-roots g"),
          (FFirstRoot, GBetweenRoots, "both moved"),
        })
        {
          var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), f), g));
          var rightAssociated = Print(BindForest(ParseForest(tree), Compose(f, g)));

          Assert.AreEqual(leftAssociated, rightAssociated, $"pointed associativity, {label} [{tree}]");
        }
      }
    }

    [TestMethod]
    public void MonadLaw_Associativity_EmptyHeavySelectors()
    {
      // Promotion inside promotion: f empties interior nodes, g empties expansion roots --
      // the phantom rides Where's rule through both layers.
      Func<string, List<TreeModel>> emptier = value => value == "b" || value == "c" ? EmptyPointed() : ReturnPointed(value + "'");
      Func<string, List<TreeModel>> rootEater = value => value.EndsWith("'") && value.StartsWith("a") ? EmptyPointed() : ParseForest($"{value}z({Slot})");

      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), emptier), rootEater));
        var rightAssociated = Print(BindForest(ParseForest(tree), Compose(emptier, rootEater)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"empty-heavy pointed associativity [{tree}]");
      }
    }

    // --------------------------------------------------- the slot-OPTIONAL system
    // Expansions may carry NO slot: the node's rewritten children are DROPPED -- the
    // vanish rule. Arity {0, 1} associative ⇒ {Where, PruneSubtreesWhere, PruneDescendantsWhere} are all
    // bind-derivable ("maximize derivable reshapings," fulfilled).

    [TestMethod]
    public void MonadLaw_Associativity_MixedSlotArity()
    {
      Func<string, List<TreeModel>> f = value =>
        value == "b" ? SlotlessEmpty()
        : value == "d" ? SlotlessLeaf(value + "†")
        : value == "c" ? ParseForest($"c1,c2(c3,{Slot})")
        : ParseForest($"{value}1({value}2,{Slot})");

      Func<string, List<TreeModel>> g = value =>
        value.EndsWith("2") ? SlotlessEmpty()
        : value.EndsWith("†") ? SlotlessLeaf(value + "!")
        : value.EndsWith("3") ? ParseForest($"{value}L,{value}R({Slot})")
        : ParseForest($"{value}x({Slot})");

      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), f), g));
        var rightAssociated = Print(BindForest(ParseForest(tree), Compose(f, g)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"mixed slot-arity associativity [{tree}]");
      }
    }

    [TestMethod]
    public void MonadLaw_Associativity_SlotErasedByASlotlessExpansion()
    {
      // The sharpest corner: f points INSIDE a subtree that g then prunes -- the phantom
      // itself is dropped, so the composite goes slotless and the outer children vanish.
      Func<string, List<TreeModel>> f = value =>
        value == "c" ? ParseForest($"keep,cut(inner({Slot}))")
        : ReturnPointed(value + "'");

      Func<string, List<TreeModel>> g = value =>
        value == "cut" ? SlotlessEmpty()
        : ReturnPointed(value + "!");

      foreach (var tree in Trees)
      {
        var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), f), g));
        var rightAssociated = Print(BindForest(ParseForest(tree), Compose(f, g)));

        Assert.AreEqual(leftAssociated, rightAssociated, $"slot-erased-by-prune associativity [{tree}]");
      }
    }

    // The old rule's failure, retired: under pointed attachment the two association orders
    // agree on the exact tree that broke the fixed rule.
    [TestMethod]
    public void TheFormerCounterexample_NowConverges()
    {
      const string tree = "a(b(d,e),c(f,g))";

      var leftAssociated = Print(BindForest(BindForest(ParseForest(tree), F), G));
      var rightAssociated = Print(BindForest(ParseForest(tree), Compose(F, G)));

      Assert.AreEqual(leftAssociated, rightAssociated, "the pinned counterexample converges under pointed attachment");
    }
  }
}
