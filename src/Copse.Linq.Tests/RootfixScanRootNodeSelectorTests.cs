using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  // The (rootNodeSelector, accumulator) overload -- the BYPASS INSTRUMENT (THE NORTH STAR,
  // 2026-08-05: boundary flavors mean the same thing on both tiers): every root's ACCUMULATION
  // is the selector's return, set directly -- the fold fires only at non-roots -- exactly as
  // the dispatch selector sets roots' arrivals directly. The wrapped accumulator rides the
  // same treenumerators the seed-form's full strategy matrix already exercises, so these
  // tests target the boundary semantics themselves.
  [TestClass]
  public class RootfixScanRootNodeSelectorTests
  {
    // Selector: each root's accumulation IS its own letter UPPERCASED (set directly, no fold);
    // accumulator: parent accumulation + node letter, at non-roots only. A shared seed could
    // never produce two different root values, so the multi-root rows prove per-root seeding.
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""               , ""                       },
          new [] { "a"              , "A"                      },
          new [] { "a,b,c"          , "A,B,C"                  },
          new [] { "a(b,c)"         , "A(Ab,Ac)"               },
          new [] { "a(b(c))"        , "A(Ab(Abc))"             },
          new [] { "a(b,c),d(e,f)"  , "A(Ab,Ac),D(De,Df)"      },
          new [] { "a,b(c),d(e(f))" , "A,B(Bc),D(De(Def))"     },
        };
    }

    public static string GetTestDisplayName(MethodInfo methodInfo, object[] data)
    {
      return
        data[0].ToString() == ""
        ? "<empty-string>"
        : data[0].ToString();
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EachRootSeedsItsOwnAccumulation_DepthFirst(string treeString, string expectedTreeString)
    {
      EachRootSeedsItsOwnAccumulation(treeString, expectedTreeString, TreeTraversalStrategy.DepthFirst);
    }

    [TestMethod]
    [DynamicData(nameof(GetTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void EachRootSeedsItsOwnAccumulation_BreadthFirst(string treeString, string expectedTreeString)
    {
      EachRootSeedsItsOwnAccumulation(treeString, expectedTreeString, TreeTraversalStrategy.BreadthFirst);
    }

    private static void EachRootSeedsItsOwnAccumulation(
      string treeString,
      string expectedTreeString,
      TreeTraversalStrategy treeTraversalStrategy)
    {
      var expected =
        TreeSerializer
        .DeserializeDepthFirstTree(expectedTreeString)
        .GetTraversal(treeTraversalStrategy)
        .ToArray();

      var actual =
        TreeSerializer
        .DeserializeDepthFirstTree(treeString)
        .RootfixScan(
          root => root.ToUpperInvariant(),
          (accumulate, node) => accumulate + node)
        .Select(pairing => pairing.Accumulate)
        .GetTraversal(treeTraversalStrategy)
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SeedAndSelectorFlavors_AreDifferentInstruments_MirroringTheDispatchTier()
    {
      // THE NORTH STAR's scan half (2026-08-05), mirroring RootfixDispatchTests' pin: the seed
      // is the virtual root's arrival, TRANSFORMED by the fold at every node; the selector
      // sets each root's accumulation DIRECTLY, bypassing the fold. So the seed form is NOT
      // the constant selector -- deliberately, so a future "consistency fix" is a decision.
      string Accumulator(string accumulate, string node) =>
        accumulate + node;

      var seedForm =
        TreeSerializer
        .DeserializeDepthFirstTree("a,b")
        .RootfixScan("s", Accumulator)
        .PreorderTraversal()
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      var selectorForm =
        TreeSerializer
        .DeserializeDepthFirstTree("a,b")
        .RootfixScan(_ => "s", Accumulator)
        .PreorderTraversal()
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(new[] { "sa", "sb" }, seedForm, "the seed is the virtual root's arrival -- the fold transforms it at each root");
      CollectionAssert.AreEqual(new[] { "s", "s" }, selectorForm, "the selector sets each root's accumulation directly");
    }

    [TestMethod]
    public void Accumulator_FiresOnlyAtNonRoots_UnderTheSelectorFlavor()
    {
      // The bypass instrument, pinned as a COUNT: roots take the selector's return directly,
      // so the fold fires once per NON-ROOT only (6 nodes, 2 roots -> 4 invocations). Under
      // the SEED flavor the fold fires at every node (the two-instruments distinction).
      var accumulatorInvocations = 0;

      TreeSerializer
        .DeserializeDepthFirstTree("a(b(c),d),e(f)")
        .RootfixScan(
          root => root,
          (accumulate, node) =>
          {
            accumulatorInvocations++;
            return accumulate + node;
          })
        .PreorderTraversal()
        .ToArray();

      Assert.AreEqual(4, accumulatorInvocations);
    }

    [TestMethod]
    public void RootfixAggregate_SeedsPerRoot()
    {
      // The bypass instrument through the aggregate's delegation: a's leaves fold from a's
      // accumulation "A" (the selector's return, set directly); d's from "D".
      var leafAccumulations =
        TreeSerializer
        .DeserializeDepthFirstTree("a(b,c),d(e)")
        .RootfixAggregate(
          root => root.ToUpperInvariant(),
          (accumulate, node) => accumulate + node)
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(new[] { "Ab", "Ac", "De" }, leafAccumulations);
    }
  }
}
