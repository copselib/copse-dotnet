using Copse.Core;
using Copse.Linq.Treenumerables;
using Copse.SimpleSerializer;
using Copse.Treenumerables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // Pins for docs/OPERATOR_FUSION_DESIGN.md: fusion must be observably invisible (identical
  // visit streams, identical per-lambda order) and must actually HAPPEN (collapsed layers,
  // once-per-node selector evaluation). The force-unfused controls: the positional Where
  // overload never fuses with its own kind, and Tree.Defer's delegating wrapper is not
  // fusable, so inserting it breaks any chain without changing semantics.
  [TestClass]
  public class FusionTests
  {
    private static ITreenumerable<string> Tree(string serialized) =>
      TreeSerializer.DeserializeDepthFirstTree(serialized);

    [TestMethod]
    public void ValueWheres_Fuse_AndMatchTheStackedPipeline()
    {
      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        var fused = Tree("a(b(d,e,f),c)")
          .Where(n => n != "b")
          .Where(n => n != "e")
          .GetTraversal(strategy).ToArray();

        var stacked = Tree("a(b(d,e,f),c)")
          .Where((n, position) => n != "b")
          .Where((n, position) => n != "e")
          .GetTraversal(strategy).ToArray();

        CollectionAssert.AreEqual(stacked, fused, $"{strategy}");
      }
    }

    [TestMethod]
    public void ValueWheres_CollapseToOneWrapper()
    {
      var fused = Tree("a(b,c)").Where(n => n != "b").Where(n => n != "c").Where(n => n != "z");

      Assert.IsInstanceOfType(fused, typeof(WhereTreenumerable<string>));
    }

    [TestMethod]
    public void ValueWheres_PreserveLambdaOrderAndEarlyExit()
    {
      var invocations = new List<string>();

      Tree("a(b(c))")
        .Where(n => { invocations.Add($"p1({n})"); return n != "b"; })
        .Where(n => { invocations.Add($"p2({n})"); return true; })
        .GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray();

      // Per node, in source order; p2 never sees b (rejected by p1 -- exactly the node the
      // stacked pipeline's second layer never receives).
      CollectionAssert.AreEqual(
        new[] { "p1(a)", "p2(a)", "p1(b)", "p1(c)", "p2(c)" },
        invocations);
    }

    // The a(b(c)) experiment from the design doc, pinned: a compound positional predicate and
    // stacked Wheres are DIFFERENT operations, because the second layer's predicate sees the
    // first layer's emitted labels (c promoted to depth 1), while a compound predicate judges
    // c at its source depth 2.
    [TestMethod]
    public void CompoundPredicate_IsNotStackedWheres()
    {
      var compound = Tree("a(b(c))")
        .Where((n, position) => n != "b" && position.Depth <= 1)
        .GetTraversal(TreeTraversalStrategy.DepthFirst).Select(visit => visit.Node).Distinct().ToArray();

      var stacked = Tree("a(b(c))")
        .Where(n => n != "b")
        .Where((n, position) => position.Depth <= 1)
        .GetTraversal(TreeTraversalStrategy.DepthFirst).Select(visit => visit.Node).Distinct().ToArray();

      CollectionAssert.AreEqual(new[] { "a" }, compound, "compound judges c at source depth 2");
      CollectionAssert.AreEqual(new[] { "a", "c" }, stacked, "stacked judges c at its emitted depth 1");
    }

    [TestMethod]
    public void SelectThenWhere_Fuses_AndMatchesTheStackedPipeline()
    {
      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        var fused = Tree("a(b(d,e,f),c)")
          .Select(n => n + "!")
          .Where(n => n != "b!")
          .GetTraversal(strategy).ToArray();

        // Tree.Defer's wrapper is not fusable: same pipeline, forced to stack.
        var stacked = Copse.Treenumerables.Tree.Defer(() => Tree("a(b(d,e,f),c)").Select(n => n + "!"))
          .Where(n => n != "b!")
          .GetTraversal(strategy).ToArray();

        CollectionAssert.AreEqual(stacked, fused, $"{strategy}");
      }
    }

    [TestMethod]
    public void PositionalWhere_OverSelect_StillFuses_AndSeesSourcePositions()
    {
      // Select's emission boundary is the identity on positions, so even the positional Where
      // may cross it; the positions it sees are unchanged by the projection.
      var seenDepths = new List<int>();

      Tree("a(b(c))")
        .Select(n => n + "!")
        .Where((n, position) => { seenDepths.Add(position.Depth); return true; })
        .GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray();

      CollectionAssert.AreEqual(new[] { 0, 1, 2 }, seenDepths);
    }

    // The fused Select-Where evaluates the selector at the TEST SITE -- once per tested node --
    // where the unfused Select wrapper re-projects on every visit. Selector invocation COUNT is
    // declared unspecified (purity expected); this pins the fused path's actual behavior so a
    // regression is a decision, not an accident.
    [TestMethod]
    public void FusedSelectWhere_EvaluatesSelectorOncePerNode()
    {
      var selectorCalls = 0;

      Tree("a(b(d,e,f),c)")
        .Select(n => { selectorCalls++; return n + "!"; })
        .Where(n => n != "b!")
        .GetTraversal(TreeTraversalStrategy.DepthFirst).ToArray();

      Assert.AreEqual(6, selectorCalls);
    }

    [TestMethod]
    public void ValueSelects_ComposeThroughTheFusedChain()
    {
      var fused = Tree("a(b,c)")
        .Select(n => n + "1")
        .Select(n => n + "2")
        .Where(n => n != "b12")
        .GetTraversal(TreeTraversalStrategy.DepthFirst).Select(visit => visit.Node).Distinct().ToArray();

      CollectionAssert.AreEqual(new[] { "a12", "c12" }, fused);
    }
  }
}
