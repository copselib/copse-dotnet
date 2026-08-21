using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static Copse.Linq.Tests.PointedBindReferenceModel;

namespace Copse.Linq.Tests
{
  // The shipped SelectMany against its oracle: every (tree, selector) in the corpus must
  // produce exactly the reference model's forest -- as a depth-first visit stream AND as a
  // breadth-first one, positions and visit counts included, lockstepped against the flat
  // family's treenumerators over the expected tree (themselves conformance-pinned to the
  // engine). Then the derived operators: the four special values must reproduce the real
  // Select, Where, PruneBefore, and PruneAfter byte for byte. Then the streaming contract:
  // a dropped subtree is never pulled, and nothing is pulled ahead of its emission.
  [TestClass]
  public class SelectManyOperatorTests
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

    // ----------------------------------------------------------- the selector corpus

    // Each real selector has its model twin: the same placements spelled with the phantom.
    private sealed class SelectorPair
    {
      public SelectorPair(string name, Func<string, Expansion<string>> real, Func<string, List<TreeModel>> model)
      {
        Name = name;
        Real = real;
        Model = model;
      }

      public string Name { get; }
      public Func<string, Expansion<string>> Real { get; }
      public Func<string, List<TreeModel>> Model { get; }
    }

    private static ITreenumerable<string> Forest(string text) => TreeSerializer.DeserializeDepthFirstTree(text);

    private static Expansion<string> AfterRoots(string forest) => Expansion.Of(Forest(forest), SlotPlacement.AfterRoots);

    private static Expansion<string> UnderLastRoot(string forest) => Expansion.Of(Forest(forest), SlotPlacement.UnderLastRoot);

    private static Expansion<string> Slotless(string forest) => Expansion.Of(Forest(forest), SlotPlacement.None);

    private static List<TreeModel> ModelAfterRoots(string forest) => ParseForest(forest + "," + Slot);

    private static List<TreeModel> ModelUnderLastRoot(string forest)
    {
      var model = ParseForest(forest);

      if (model.Count == 0)
        return EmptyPointed();

      model[model.Count - 1].Children.Add(new TreeModel(Slot));
      return model;
    }

    private static readonly SelectorPair[] Selectors =
    {
      new SelectorPair("quartet mix",
        value => value == "b" ? Expansion.Promote<string>() : value == "c" ? Expansion.Drop<string>() : value == "d" ? Expansion.Leaf(value + "!") : Expansion.Return(value + "'"),
        value => value == "b" ? EmptyPointed() : value == "c" ? SlotlessEmpty() : value == "d" ? SlotlessLeaf(value + "!") : ReturnPointed(value + "'")),
      new SelectorPair("forests after roots",
        value => value == "c" ? AfterRoots("c1,c2(c3)") : value == "b" ? AfterRoots("") : Expansion.Return(value + "'"),
        value => value == "c" ? ModelAfterRoots("c1,c2(c3)") : value == "b" ? EmptyPointed() : ReturnPointed(value + "'")),
      new SelectorPair("forests under last root",
        value => value == "c" ? UnderLastRoot("c1,c2(c3)") : value == "a" ? UnderLastRoot("a1(a2,a3)") : value == "e" ? Slotless("e1(e2),e3") : Expansion.Return(value + "'"),
        value => value == "c" ? ModelUnderLastRoot("c1,c2(c3)") : value == "a" ? ModelUnderLastRoot("a1(a2,a3)") : value == "e" ? ParseForest("e1(e2),e3") : ReturnPointed(value + "'")),
      new SelectorPair("the 2026-08-12 counterexample's first selector, pointed under the last root",
        value => value == "b" ? Expansion.Promote<string>() : value == "c" ? UnderLastRoot("c1,c2(c3)") : UnderLastRoot($"{value}1({value}2)"),
        value => value == "b" ? EmptyPointed() : value == "c" ? ModelUnderLastRoot("c1,c2(c3)") : ModelUnderLastRoot($"{value}1({value}2)")),
    };

    private static readonly SelectorPair SecondSelector = new SelectorPair("generated-value selector",
      value => value.EndsWith("2") ? Expansion.Promote<string>() : value.EndsWith("3") ? AfterRoots($"{value}L,{value}R") : value == "d'" ? Expansion.Drop<string>() : Expansion.Return(value + "x"),
      value => value.EndsWith("2") ? EmptyPointed() : value.EndsWith("3") ? ModelAfterRoots($"{value}L,{value}R") : value == "d'" ? SlotlessEmpty() : ReturnPointed(value + "x"));

    // ------------------------------------------------------------------ conformance

    [TestMethod]
    public void Conformance_EveryTreeAndSelector_ReproducesTheModel_BothDimensions()
    {
      foreach (var tree in Trees)
      {
        foreach (var pair in Selectors)
        {
          var expected = Print(BindForest(ParseForest(tree), pair.Model));
          var actual = Forest(tree).SelectMany(pair.Real);

          Assert.AreEqual(expected, actual.SerializeDepthFirstTree(), $"shape [{tree}] via {pair.Name}");
          AssertVisitStreamsAgree(Forest(expected), actual, $"[{tree}] via {pair.Name}");
        }
      }
    }

    [TestMethod]
    public void Conformance_StackedBinds_ReproduceTheModel()
    {
      foreach (var tree in Trees)
      {
        foreach (var pair in Selectors)
        {
          var expected = Print(BindForest(BindForest(ParseForest(tree), pair.Model), SecondSelector.Model));
          var actual = Forest(tree).SelectMany(pair.Real).SelectMany(SecondSelector.Real);

          Assert.AreEqual(expected, actual.SerializeDepthFirstTree(), $"stacked shape [{tree}] via {pair.Name}");
          AssertVisitStreamsAgree(Forest(expected), actual, $"stacked [{tree}] via {pair.Name}");
        }
      }
    }

    [TestMethod]
    public void Conformance_TheFormerCounterexample_IsNowLawful_OnTheRealOperator()
    {
      // The pinned 2026-08-12 failure, run through the shipped operator twice; the model's
      // left-nested result is the only result, and it matches.
      const string tree = "a(b(d,e),c(f,g))";
      var pair = Selectors[3];

      var expected = Print(BindForest(BindForest(ParseForest(tree), pair.Model), SecondSelector.Model));

      Assert.AreEqual(expected, Forest(tree).SelectMany(pair.Real).SelectMany(SecondSelector.Real).SerializeDepthFirstTree());
    }

    // ------------------------------------------------------------ the derived operators

    [TestMethod]
    public void Derived_Return_IsSelect()
    {
      Func<string, string> map = value => value + "!";

      foreach (var tree in Trees)
        Assert.AreEqual(
          Forest(tree).Select(map).SerializeDepthFirstTree(),
          Forest(tree).SelectMany(value => Expansion.Return(map(value))).SerializeDepthFirstTree(),
          $"Select [{tree}]");
    }

    [TestMethod]
    public void Derived_ReturnOrPromote_IsWhere()
    {
      Func<string, bool> keep = value => value != "b" && value != "e";

      foreach (var tree in Trees)
        Assert.AreEqual(
          Forest(tree).Where(keep).SerializeDepthFirstTree(),
          Forest(tree).SelectMany(value => keep(value) ? Expansion.Return(value) : Expansion.Promote<string>()).SerializeDepthFirstTree(),
          $"Where [{tree}]");
    }

    [TestMethod]
    public void Derived_ReturnOrDrop_IsPruneBefore()
    {
      Func<string, bool> prune = value => value == "b" || value == "e";

      foreach (var tree in Trees)
        Assert.AreEqual(
          Forest(tree).PruneBefore(prune).SerializeDepthFirstTree(),
          Forest(tree).SelectMany(value => prune(value) ? Expansion.Drop<string>() : Expansion.Return(value)).SerializeDepthFirstTree(),
          $"PruneBefore [{tree}]");
    }

    [TestMethod]
    public void Derived_ReturnOrLeaf_IsPruneAfter()
    {
      Func<string, bool> prune = value => value == "b" || value == "c";

      foreach (var tree in Trees)
        Assert.AreEqual(
          Forest(tree).PruneAfter(prune).SerializeDepthFirstTree(),
          Forest(tree).SelectMany(value => prune(value) ? Expansion.Leaf(value) : Expansion.Return(value)).SerializeDepthFirstTree(),
          $"PruneAfter [{tree}]");
    }

    // ------------------------------------------------------------ the streaming contract

    [TestMethod]
    public void Streaming_ADroppedSubtreeIsNeverPulled()
    {
      var scheduled = new List<string>();

      var result = Forest("a(b(c(d)),e)")
        .Do(visit => { if (visit.Mode == TreenumeratorMode.SchedulingNode) scheduled.Add(visit.Node); })
        .SelectMany(value => value == "b" ? Expansion.Drop<string>() : Expansion.Return(value))
        .SerializeDepthFirstTree();

      Assert.AreEqual("a(e)", result);
      CollectionAssert.AreEqual(new[] { "a", "b", "e" }, scheduled, "b's subtree was skipped at the source, not pulled and discarded");
    }

    [TestMethod]
    public void Streaming_NothingIsPulledAheadOfItsEmission()
    {
      // Every source schedule is immediately followed by that node's replacement appearing
      // -- no source node is pulled while an earlier replacement is still owed.
      var log = new List<string>();

      var stream = Forest("a(b(d,e),c)")
        .Do(visit => { if (visit.Mode == TreenumeratorMode.SchedulingNode) log.Add($"pull {visit.Node}"); })
        .SelectMany(value => Expansion.Return(value + "'"));

      using (var treenumerator = stream.GetDepthFirstTreenumerator())
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
            log.Add($"emit {treenumerator.Node}");

      CollectionAssert.AreEqual(
        new[] { "pull a", "emit a'", "pull b", "emit b'", "pull d", "emit d'", "pull e", "emit e'", "pull c", "emit c'" },
        log);
    }

    // ---------------------------------------------------------------------- helpers

    private static void AssertVisitStreamsAgree(ITreenumerable<string> expected, ITreenumerable<string> actual, string label)
    {
      CollectionAssert.AreEqual(Drain(expected.GetDepthFirstTreenumerator()), Drain(actual.GetDepthFirstTreenumerator()), $"depth-first visit stream {label}");
      CollectionAssert.AreEqual(Drain(expected.GetBreadthFirstTreenumerator()), Drain(actual.GetBreadthFirstTreenumerator()), $"breadth-first visit stream {label}");
    }

    private static List<string> Drain(ITreenumerator<string> treenumerator)
    {
      var events = new List<string>();

      using (treenumerator)
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
          events.Add($"{(treenumerator.Mode == TreenumeratorMode.SchedulingNode ? "S" : "V")}{treenumerator.VisitCount} {treenumerator.Node}@{treenumerator.Position.Depth}.{treenumerator.Position.SiblingIndex}");

      return events;
    }
  }
}
