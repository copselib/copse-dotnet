using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Copse.Linq.Tests
{
  // The closure fact behind the collapse lattice (OPERATOR_COMPOSITION_DESIGN.md): the four
  // special expansions -- Return, Promote, Drop, Leaf, the library's own reshapings as
  // theorems -- are CLOSED under Kleisli composition. Bind an expansion of one kind through a
  // selector of another and the composite is again one of the four:
  //
  //     f(x) \ g(a)   Return(b)   Promote   Drop   Leaf(b)
  //     Return(a)     Return(b)   Promote   Drop   Leaf(b)
  //     Leaf(a)       Leaf(b)     Drop      Drop   Leaf(b)
  //     Promote       Promote     Promote   Promote Promote
  //     Drop          Drop        Drop      Drop   Drop
  //
  // The phantom's inheritance does the work: Leaf then Promote is Drop because Leaf already
  // dropped the slot the promotion would have handed the children to. So any chain of
  // Select/Where/PruneBefore/PruneAfter is ONE quartet-valued selector, pointwise -- the
  // struct-composed arrow of the collapse lattice IS Kleisli composition restricted to the
  // quartet, a sub-monoid of the Kleisli category. Pinned on the real operator: stacking two
  // quartet-valued binds equals one bind of the table's entry, values composed.
  [TestClass]
  public class QuartetKleisliClosureTests
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

    private enum Quartet
    {
      Return,
      Promote,
      Drop,
      Leaf,
    }

    private static readonly Quartet[] All = { Quartet.Return, Quartet.Promote, Quartet.Drop, Quartet.Leaf };

    private static Expansion<string> Spell(Quartet kind, string value)
    {
      switch (kind)
      {
        case Quartet.Return: return Expansion.Return(value);
        case Quartet.Promote: return Expansion.Promote<string>();
        case Quartet.Drop: return Expansion.Drop<string>();
        default: return Expansion.Leaf(value);
      }
    }

    // The table.
    private static Quartet Then(Quartet first, Quartet second)
    {
      switch (first)
      {
        case Quartet.Return: return second;
        case Quartet.Leaf: return second == Quartet.Return || second == Quartet.Leaf ? Quartet.Leaf : Quartet.Drop;
        default: return first;                           // Promote and Drop absorb: nothing reaches g
      }
    }

    private static ITreenumerable<string> Forest(string text) => TreeSerializer.DeserializeDepthFirstTree(text);

    [TestMethod]
    public void TheSixteenCases_StackedBindsEqualTheTableEntry()
    {
      foreach (var tree in Trees)
        foreach (var first in All)
          foreach (var second in All)
          {
            var stacked = Forest(tree)
              .SelectMany(value => Spell(first, value + "'"))
              .SelectMany(value => Spell(second, value + "!"))
              .SerializeDepthFirstTree();

            var composite = Forest(tree)
              .SelectMany(value => Spell(Then(first, second), value + "'!"))
              .SerializeDepthFirstTree();

            Assert.AreEqual(composite, stacked, $"{first} then {second} [{tree}]");
          }
    }

    [TestMethod]
    public void Pointwise_SelectorsChoosingPerNode_ComposeByTheTablePerNode()
    {
      // Each selector picks its kind from the node; the composite picks the table's entry
      // from the two kinds at that node. g sees f's value, whose first letter is the node's.
      Func<string, Quartet> firstKind = value =>
        value.StartsWith("b") ? Quartet.Promote : value.StartsWith("c") ? Quartet.Leaf : value.StartsWith("e") ? Quartet.Drop : Quartet.Return;
      Func<string, Quartet> secondKind = value =>
        value.StartsWith("c") ? Quartet.Promote : value.StartsWith("d") ? Quartet.Leaf : value.StartsWith("f") ? Quartet.Drop : Quartet.Return;

      foreach (var tree in Trees)
      {
        var stacked = Forest(tree)
          .SelectMany(value => Spell(firstKind(value), value + "'"))
          .SelectMany(value => Spell(secondKind(value), value + "!"))
          .SerializeDepthFirstTree();

        var composite = Forest(tree)
          .SelectMany(value => Spell(Then(firstKind(value), secondKind(value)), value + "'!"))
          .SerializeDepthFirstTree();

        Assert.AreEqual(composite, stacked, $"pointwise closure [{tree}]");
      }
    }

    [TestMethod]
    public void TheTableIsAMonoid_ReturnIsTheUnit_AndThenIsAssociative()
    {
      foreach (var kind in All)
      {
        Assert.AreEqual(kind, Then(Quartet.Return, kind), $"Return then {kind}");
        Assert.AreEqual(kind, Then(kind, Quartet.Return), $"{kind} then Return");
      }

      foreach (var first in All)
        foreach (var second in All)
          foreach (var third in All)
            Assert.AreEqual(
              Then(Then(first, second), third),
              Then(first, Then(second, third)),
              $"({first} {second}) {third}");
    }
  }
}
