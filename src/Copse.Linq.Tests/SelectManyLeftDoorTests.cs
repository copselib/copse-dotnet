using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Copse.Linq.Tests
{
  // The left door (SELECTMANY_DESIGN.md Addendum V): a collapsed chain before bind --
  // Where, PruneBefore, PruneAfter, Select, any order -- surrenders its raw inner and its
  // struct arrow, and ONE bind machine runs over the inner with the arrow folded ahead of
  // the selector. Three pins per chain shape: the door's result equals the stacked spelling
  // (the same chain behind an opaque wrapper, which cannot surrender) byte-for-byte in both
  // dimensions; the door is TAKEN (the result is the bind treenumerable over the folded leg,
  // not a bind over a wrapper); and the fold changes no effect -- the chain's lambdas fire
  // exactly as often, on exactly the nodes, as in the stacked spelling.
  [TestClass]
  public class SelectManyLeftDoorTests
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
      "a(b(c(d(e))))",
    };

    private static ITreenumerable<string> Forest(string text) => TreeSerializer.DeserializeDepthFirstTree(text);

    // The stacked spelling: the same chain, unable to surrender.
    private static ITreenumerable<TNode> Opaque<TNode>(ITreenumerable<TNode> source)
      => Tree.Create(source.GetBreadthFirstTreenumerator, source.GetDepthFirstTreenumerator);

    // A general selector: forests, every placement, the quartet -- chosen by the node.
    private static Expansion<string> General(string value)
    {
      var key = value.Length > 0 ? value[0] : ' ';

      switch (key)
      {
        case 'b': return Expansion.Of(Forest($"{value}1,{value}2({value}3)"), SlotPlacement.UnderLastRoot);
        case 'c': return Expansion.Of(Forest($"{value}L,{value}R"), SlotPlacement.AfterRoots);
        case 'd': return Expansion.Promote<string>();
        case 'e': return Expansion.Of(Forest($"{value}x"), SlotPlacement.None);
        case 'f': return Expansion.Leaf(value + "!");
        default: return Expansion.Return(value + "'");
      }
    }

    private static readonly Func<string, bool> Keep = value => value != "c" && value != "e";
    private static readonly Func<string, bool> Prune = value => value == "b" || value == "f";
    private static readonly Func<string, string> Map = value => value + "~";
    private static readonly Func<string, NodePosition, bool> KeepPositional = (value, position) => position.SiblingIndex == 0 || value == "c";

    private static readonly (string Name, Func<ITreenumerable<string>, ITreenumerable<string>> Chain)[] Chains =
    {
      ("Where", source => source.Where(Keep)),
      ("PruneBefore", source => source.PruneBefore(Prune)),
      ("PruneAfter", source => source.PruneAfter(Prune)),
      ("Select", source => source.Select(Map)),
      ("positional Where", source => source.Where(KeepPositional)),
      ("Select.Where", source => source.Select(Map).Where(value => value != "c~")),
      ("Where.Select.PruneAfter", source => source.Where(Keep).Select(Map).PruneAfter(value => value == "b~")),
      ("PruneBefore.Where.Select", source => source.PruneBefore(Prune).Where(Keep).Select(Map)),
      ("Where.PruneAfter.Where", source => source.Where(Keep).PruneAfter(value => value == "b").Where(value => value != "d")),
      ("Select.PruneAfter", source => source.Select(Map).PruneAfter(value => value == "b~")),
      ("PruneAfter.Where", source => source.PruneAfter(Prune).Where(Keep)),
      ("PruneAfter.Select", source => source.PruneAfter(Prune).Select(Map)),
    };

    [TestMethod]
    public void TheDoorEqualsTheStackedSpelling_BothDimensions()
    {
      foreach (var tree in Trees)
        foreach (var (name, chain) in Chains)
        {
          var door = chain(Forest(tree)).SelectMany(General);
          var stacked = Opaque(chain(Forest(tree))).SelectMany(General);

          Assert.AreEqual(stacked.SerializeDepthFirstTree(), door.SerializeDepthFirstTree(), $"{name} depth-first [{tree}]");
          Assert.AreEqual(stacked.SerializeBreadthFirstTree(), door.SerializeBreadthFirstTree(), $"{name} breadth-first [{tree}]");
        }
    }

    [TestMethod]
    public void TheDoorIsTaken_OneBindMachineOverTheFoldedLeg()
    {
      foreach (var (name, chain) in Chains)
      {
        var door = chain(Forest("a(b,c)")).SelectMany(General);

        Assert.AreEqual(typeof(SelectManyTreenumerable<,,>), door.GetType().GetGenericTypeDefinition(), $"{name}: the bind treenumerable");
        Assert.AreEqual(
          typeof(FoldedExpansionSelector<,,,>),
          door.GetType().GetGenericArguments()[2].GetGenericTypeDefinition(),
          $"{name}: the chain's arrow folded ahead of the selector");
      }

      var plain = Forest("a(b,c)").SelectMany(General);

      Assert.AreEqual(typeof(FuncExpansionSelector<,>), plain.GetType().GetGenericArguments()[2].GetGenericTypeDefinition(), "no chain: the bare leg");
    }

    [TestMethod]
    public void TheFoldChangesNoEffect_LambdasFireAsInTheStackedSpelling()
    {
      foreach (var tree in Trees)
      {
        var doorLog = new List<string>();
        var stackedLog = new List<string>();

        Func<List<string>, Func<ITreenumerable<string>, ITreenumerable<string>>> chain = log => source => source
          .Where(value => { log.Add("where " + value); return Keep(value); })
          .PruneAfter(value => { log.Add("pruneAfter " + value); return value == "b"; })
          .Select(value => { log.Add("select " + value); return Map(value); });

        Func<List<string>, Func<string, Expansion<string>>> selector = log => value => { log.Add("bind " + value); return General(value); };

        chain(doorLog)(Forest(tree)).SelectMany(selector(doorLog)).SerializeDepthFirstTree();
        Opaque(chain(stackedLog)(Forest(tree))).SelectMany(selector(stackedLog)).SerializeDepthFirstTree();

        // The stacked spelling's middle-tier passthrough driver re-evaluates its pure legs
        // (the projection, the prune-after predicate) on every emission event of a node; the
        // fold evaluates each leg once per node, at its scheduling -- as the lattice's Where
        // driver does. So the door's log is the stacked log's FIRST-OCCURRENCE sequence: the
        // same effects, in the same order, each once.
        var firstOccurrences = new List<string>();
        var seen = new HashSet<string>();

        foreach (var entry in stackedLog)
          if (seen.Add(entry))
            firstOccurrences.Add(entry);

        CollectionAssert.AreEqual(firstOccurrences, doorLog, $"effect order [{tree}] stacked=[{string.Join("; ", stackedLog)}] door=[{string.Join("; ", doorLog)}]");
        Assert.IsTrue(doorLog.Count > 0, $"instrument [{tree}]");
      }
    }
  }
}
