using Copse;
using Copse.Core;
using Copse.SimpleSerializer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copse.Linq.Tests
{
  // The (rootNodeSelector, accumulator) overload -- LeaffixScan's structural dual -- whose point
  // is FORESTS: every root's ARRIVAL comes from the selector instead of a shared seed, and the
  // fold fires at EVERY node, roots included (arrival semantics, full participation 2026-08-04).
  // The wrapped accumulator rides the same treenumerators the seed-form's full strategy matrix
  // already exercises, so these tests target the seeding semantics themselves.
  [TestClass]
  public class RootfixScanRootNodeSelectorTests
  {
    // Selector: each root's ARRIVAL is its own letter UPPERCASED; accumulator: arrival +
    // node letter -- so a root's accumulation is fold(selector(root), root) (e.g. "Aa"), the
    // fold firing at roots exactly as it fires everywhere. A shared seed could never produce
    // two different root arrivals, so the multi-root rows prove per-root seeding.
    public static IEnumerable<object[]> GetTestData()
    {
      return new[]
        {
          new [] { ""               , ""                             },
          new [] { "a"              , "Aa"                           },
          new [] { "a,b,c"          , "Aa,Bb,Cc"                     },
          new [] { "a(b,c)"         , "Aa(Aab,Aac)"                  },
          new [] { "a(b(c))"        , "Aa(Aab(Aabc))"                },
          new [] { "a(b,c),d(e,f)"  , "Aa(Aab,Aac),Dd(Dde,Ddf)"      },
          new [] { "a,b(c),d(e(f))" , "Aa,Bb(Bbc),Dd(Dde(Ddef))"     },
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
    public void SeedOverload_IsTheConstantRootSelector()
    {
      // Arrival semantics make the flavors' relationship exact: the seed form IS the selector
      // form with a constant selector -- both hand every root the same arrival, and the fold
      // fires at every node under both.
      foreach (var treeString in GetTestData().Select(data => (string)data[0]))
      {
        string Accumulator(string accumulate, string node) =>
          accumulate + node;

        var seedForm =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .RootfixScan("s", Accumulator)
          .PreorderTraversal()
          .ToArray();

        var selectorForm =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .RootfixScan(
            _ => "s",
            Accumulator)
          .PreorderTraversal()
          .ToArray();

        CollectionAssert.AreEqual(seedForm, selectorForm, $"mismatch for {treeString}");
      }
    }

    [TestMethod]
    public void Accumulator_FiresAtEveryNode_RootsIncluded()
    {
      // Full participation on the fold tier, pinned as a COUNT: the fold fires once per node
      // -- roots included, their arrival being the selector's return -- so 6 nodes means 6
      // invocations. (The prior semantics skipped roots: 4.)
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

      Assert.AreEqual(6, accumulatorInvocations);
    }

    [TestMethod]
    public void RootfixAggregate_SeedsPerRoot()
    {
      // Arrival semantics: a's leaves fold from a's accumulation "Aa"; d's from "Dd".
      var leafAccumulations =
        TreeSerializer
        .DeserializeDepthFirstTree("a(b,c),d(e)")
        .RootfixAggregate(
          root => root.ToUpperInvariant(),
          (accumulate, node) => accumulate + node)
        .Select(pairing => pairing.Accumulate)
        .ToArray();

      CollectionAssert.AreEqual(new[] { "Aab", "Aac", "Dde" }, leafAccumulations);
    }
  }
}
