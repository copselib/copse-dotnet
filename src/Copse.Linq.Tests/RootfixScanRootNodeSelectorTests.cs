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
  // is FORESTS: every root seeds its own accumulation instead of sharing one seed. The wrapped
  // accumulator rides the same treenumerators the seed-form's full strategy matrix already
  // exercises, so these tests target the seeding semantics themselves.
  [TestClass]
  public class RootfixScanRootNodeSelectorTests
  {
    // Selector: each root seeds as its own letter UPPERCASED; accumulator: parent accumulation +
    // node letter. A shared seed could never produce two different root values, so the multi-root
    // rows prove per-root seeding.
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
          (parentAccumulation, nodeContext) => parentAccumulation.Node + nodeContext.Node,
          rootContext => rootContext.Node.ToUpperInvariant())
        .GetTraversal(treeTraversalStrategy)
        .ToArray();

      CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SeedOverload_IsTheConstantRootSelector()
    {
      // The single-seed form must equal the selector form whose selector replays the seed fold --
      // the exact accumulator invocation the engines make at a root.
      foreach (var treeString in GetTestData().Select(data => (string)data[0]))
      {
        string Accumulator(NodeContext<string> parentAccumulation, NodeContext<string> nodeContext) =>
          parentAccumulation.Node + nodeContext.Node;

        var seedForm =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .RootfixScan(Accumulator, "s")
          .PreorderTraversal()
          .ToArray();

        var selectorForm =
          TreeSerializer
          .DeserializeDepthFirstTree(treeString)
          .RootfixScan(
            Accumulator,
            rootContext => Accumulator(new NodeContext<string>("s", NodePosition.ForestRoot), rootContext))
          .PreorderTraversal()
          .ToArray();

        CollectionAssert.AreEqual(seedForm, selectorForm, $"mismatch for {treeString}");
      }
    }

    [TestMethod]
    public void Accumulator_NeverSeesAForestRootParent()
    {
      var sawForestRootParent = false;

      TreeSerializer
        .DeserializeDepthFirstTree("a(b(c),d),e(f)")
        .RootfixScan(
          (parentAccumulation, nodeContext) =>
          {
            sawForestRootParent |= parentAccumulation.Position.IsForestRoot;
            return parentAccumulation.Node + nodeContext.Node;
          },
          rootContext => rootContext.Node)
        .PreorderTraversal()
        .ToArray();

      Assert.IsFalse(sawForestRootParent);
    }

    [TestMethod]
    public void RootfixAggregate_SeedsPerRoot()
    {
      var leafAccumulations =
        TreeSerializer
        .DeserializeDepthFirstTree("a(b,c),d(e)")
        .RootfixAggregate(
          (parentAccumulation, nodeContext) => parentAccumulation.Node + nodeContext.Node,
          rootContext => rootContext.Node.ToUpperInvariant())
        .ToArray();

      CollectionAssert.AreEqual(new[] { "Ab", "Ac", "De" }, leafAccumulations);
    }
  }
}
