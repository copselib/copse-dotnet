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
    public void ValueChains_CollapseToOneWrapper_AnyOrder()
    {
      // The verdict monad's closure property: any order, any length, one wrapper.
      var fused = Tree("a(b,c)")
        .Where(n => n != "b")
        .Select(n => n + "!")
        .Where(n => n != "c!")
        .Select(n => n + "?")
        .Where(n => n != "z");

      Assert.IsInstanceOfType(fused, typeof(FusedTreenumerable<string, string, FuncVerdictSelector<string, string>>));
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

    // Both directions now splice (the consolidation fixed the asymmetry where prune-then-where
    // fused but where-then-prune stacked two wrappers): filters and prunes are the same kind of
    // verdict stage, offered through the same FuseStage hook.
    [TestMethod]
    public void WhereThenPrune_AndPruneThenWhere_BothStayOneWrapper()
    {
      var whereThenPrune = Tree("a(b(d,e),c)")
        .Where(n => n != "z")
        .PruneBefore(nodeContext => nodeContext.Node == "b");

      var pruneThenWhere = Tree("a(b(d,e),c)")
        .PruneBefore(nodeContext => nodeContext.Node == "b")
        .Where(n => n != "z");

      Assert.IsInstanceOfType(whereThenPrune, typeof(FusedTreenumerable<string, string, FuncVerdictSelector<string, string>>));
      Assert.IsInstanceOfType(pruneThenWhere, typeof(FusedTreenumerable<string, string, FuncVerdictSelector<string, string>>));

      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        CollectionAssert.AreEqual(
          pruneThenWhere.GetTraversal(strategy).ToArray(),
          whereThenPrune.GetTraversal(strategy).ToArray(),
          $"{strategy}: same stages, same tree, order-independent here (neither filters what the other sees)");
    }

    // PruneBefore is a verdict stage now (Rejected(SkipNodeAndDescendants)), so it joins the
    // fused chain; a following value-Where fuses onto it.
    [TestMethod]
    public void PruneBefore_JoinsTheFusedChain()
    {
      foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
      {
        var fused = Tree("a(b(d,e),c)")
          .PruneBefore(nodeContext => nodeContext.Node == "b")
          .Where(n => n != "z");

        Assert.IsInstanceOfType(fused, typeof(FusedTreenumerable<string, string, FuncVerdictSelector<string, string>>), "prune chain must stay fused");

        var stacked = Copse.Treenumerables.Tree.Defer(() => Tree("a(b(d,e),c)").PruneBefore(nodeContext => nodeContext.Node == "b"))
          .Where(n => n != "z")
          .GetTraversal(strategy).ToArray();

        CollectionAssert.AreEqual(stacked, fused.GetTraversal(strategy).ToArray(), $"{strategy}");
      }
    }

    // The join rule, positional-Select half: after a filter it must decline (its append point
    // becomes a real emission boundary), because it is entitled to the filtered tree's emitted
    // labels. (The join half -- fusing over a projections-only prefix -- is observably
    // invisible for pure lambdas by design; ValueSelects_ComposeThroughTheFusedChain covers
    // the composition behavior.)
    [TestMethod]
    public void PositionalSelect_AfterWhere_SeesTheEmittedLabels()
    {
      // a(b(c)), b filtered: c promotes and is EMITTED at depth 1 -- the positional selector
      // must see the relabeled coordinate, which is why it cannot join the fused chain.
      var labeled = Tree("a(b(c))")
        .Where(n => n != "b")
        .Select((n, position) => $"{n}@{position.Depth}")
        .GetTraversal(TreeTraversalStrategy.DepthFirst)
        .Select(visit => visit.Node).Distinct().ToArray();

      CollectionAssert.AreEqual(new[] { "a@0", "c@1" }, labeled);
    }

    // The PruneAfter-stage rehearsal: nothing on the surface produces accept-side verdict
    // strategies yet, but the depth-first driver's pending-merge machinery shipped with phase 2
    // and must not sit untested until the prune migration. Accept(node, SkipDescendants) = keep
    // the node, drop its subtree.
    [TestMethod]
    public void AcceptStrategies_AreHonoredDepthFirst()
    {
      var rehearsedPruneAfter = FusedTreenumerable.Create<string, string, FuncVerdictSelector<string, string>>(
        Tree("a(b(c,d),e)"),
        new FuncVerdictSelector<string, string>(nodeContext =>
          nodeContext.Node == "b"
            ? FusionVerdict<string>.Accept(nodeContext.Node, NodeTraversalStrategies.SkipDescendants)
            : FusionVerdict<string>.Accept(nodeContext.Node)),
        containsRelabelingStage: true);

      var nodes = rehearsedPruneAfter
        .GetTraversal(TreeTraversalStrategy.DepthFirst)
        .Select(visit => visit.Node).Distinct().ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b", "e" }, nodes, "b kept, its subtree dropped");
    }

    // The breadth-first half of the seam: accept-side strategies ride the pending/deferred
    // slots so they apply on the pull following the node's SCHEDULING publish, matching the
    // depth-first result (which matches what a bespoke PruneAfter produces).
    [TestMethod]
    public void AcceptStrategies_AreHonoredBreadthFirst()
    {
      var rehearsedPruneAfter = FusedTreenumerable.Create<string, string, FuncVerdictSelector<string, string>>(
        Tree("a(b(c,d),e)"),
        new FuncVerdictSelector<string, string>(nodeContext =>
          nodeContext.Node == "b"
            ? FusionVerdict<string>.Accept(nodeContext.Node, NodeTraversalStrategies.SkipDescendants)
            : FusionVerdict<string>.Accept(nodeContext.Node)),
        containsRelabelingStage: true);

      var nodes = rehearsedPruneAfter
        .GetTraversal(TreeTraversalStrategy.BreadthFirst)
        .Select(visit => visit.Node).Distinct().ToArray();

      CollectionAssert.AreEqual(new[] { "a", "b", "e" }, nodes, "b kept, its subtree dropped");
    }

    // The seam's independent oracle: the bespoke PruneAfter operator implements the same
    // semantics (keep the node, drop its subtree) through entirely different machinery, so the
    // rehearsed Accept(node, SkipDescendants) stage must match it -- both dimensions, under
    // every consumer-strategy interference aimed at every node.
    [TestMethod]
    public void AcceptStrategies_MatchTheBespokePruneAfterOracle()
    {
      var trees = new[] { "a(b(c,d),e)", "a(b(c(f),d),e)", "a(b(c)),d(e)", "a(b,c(d(e,f),g),h)" };
      var consumerStrategies = new[]
      {
        NodeTraversalStrategies.TraverseAll,
        NodeTraversalStrategies.SkipNode,
        NodeTraversalStrategies.SkipDescendants,
        NodeTraversalStrategies.SkipSiblings,
        NodeTraversalStrategies.SkipNodeAndDescendants,
        NodeTraversalStrategies.SkipNodeAndSiblings,
        NodeTraversalStrategies.SkipDescendantsAndSiblings,
        NodeTraversalStrategies.SkipAll,
      };

      foreach (var treeString in trees)
      {
        var nodes = treeString.Where(char.IsLetter).Select(character => character.ToString()).Distinct().ToArray();

        foreach (var strategy in new[] { TreeTraversalStrategy.DepthFirst, TreeTraversalStrategy.BreadthFirst })
        foreach (var pruneTarget in nodes)
        foreach (var strategyNode in nodes)
        foreach (var consumerStrategy in consumerStrategies)
        {
          NodeTraversalStrategies Selector(NodeContext<string> nodeContext)
            => nodeContext.Node == strategyNode ? consumerStrategy : NodeTraversalStrategies.TraverseAll;

          var target = pruneTarget;

          var rehearsed = FusedTreenumerable.Create<string, string, FuncVerdictSelector<string, string>>(
            Tree(treeString),
            new FuncVerdictSelector<string, string>(nodeContext =>
              nodeContext.Node == target
                ? FusionVerdict<string>.Accept(nodeContext.Node, NodeTraversalStrategies.SkipDescendants)
                : FusionVerdict<string>.Accept(nodeContext.Node)),
            containsRelabelingStage: true);

          var expected = Tree(treeString).PruneAfter(nodeContext => nodeContext.Node == target)
            .GetTraversal(strategy, Selector)
            .Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node));

          var actual = rehearsed
            .GetTraversal(strategy, Selector)
            .Select(visit => (visit.Mode, visit.Position.Depth, visit.Position.SiblingIndex, visit.VisitCount, visit.Node));

          Assert.IsTrue(
            expected.SequenceEqual(actual),
            $"{treeString} {strategy} prune={pruneTarget} consumer={strategyNode}:{consumerStrategy}");
        }
      }
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
